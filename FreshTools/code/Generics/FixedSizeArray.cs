using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace FreshTools
{
    public class FixedSizeArray<T> : IEnumerable
    {
        //private internal variables
        private const int LINEAR_SEARCH_CUTOFF = 16;
        private readonly T[] mContents;
        private int mCount;
        private IComparer<T> mComparator;
        private bool mSorted;
        private Sorter<T> mSorter;

        //public variables
        public int Count { get { return mCount; } }
        public int Capacity { get { return mContents.Length; } }
        public bool IsFull { get { return mCount >= mContents.Length; } }
        public bool IsEmpty { get { return mCount == 0; } }

        /// <summary>
        /// Expose direect access to elements in array. use with caution
        /// as making changes this way can leave the array in an unknkown state
        /// </summary>
        /// <param name="index">Index in array to return</param>
        /// <returns>Object at given index in array</returns>
        public T this[int index]
        {
            get
            {
                return mContents[index];
            }
        }

        /// <summary>
        /// Create an array of Type T at a fixed length
        /// </summary>
        /// <param name="size">Size of array to be created</param>
        public FixedSizeArray(int size)
        {
            Debug.Assert(size > 0, "Array Size cant be 0");
            // Ugh!  No generic array construction in Java.
            mContents = (T[])new T[size];
            mCount = 0;
            mSorted = false;

            mSorter = new StandardSorter<T>();
        }

        /// <summary>
        /// Create an array of Type T at a fixed length
        /// </summary>
        /// <param name="size">Size of array to be created</param>
        /// <param name="comparator">IComparer for adding/finding/sorting items</param>
        public FixedSizeArray(int size, IComparer<T> comparator)
        {
            Debug.Assert(size > 0, "Array Size cant be 0");
            mContents = (T[])new T[size];
            mCount = 0;
            mComparator = comparator;
            mSorted = false;

            mSorter = new StandardSorter<T>();
        }

        /// <summary>
        /// Returns a new array with given size. If size is smaller old items are trashed
        /// </summary>
        public FixedSizeArray<T> ChangeSize(int deltaSize)
        {
            int newCount = mCount + deltaSize;
            Debug.Assert(newCount > 0, "Array Size cant be 0");

            FixedSizeArray<T> result = new FixedSizeArray<T>(newCount, mComparator);

            int xx = 0;
            foreach (T t in mContents)
            {
                xx++;
                if (xx <= newCount)
                    result.Add(t);
                else
                    break;
            }
            return result;
        }

        /// <summary>
        /// Creates a new FixedSizeArray with the given elements sorted if possible
        /// </summary>
        /// <param name="list1">List of elements to be added</param>
        /// <param name="list2">List of elements to be added</param>
        /// <param name="listsSorted">Are these lists pre sorted</param>
        /// <param name="comparer">Comparer to sort the list now nd in the future</param>
        /// <returns>Returns a new array withe the given elements</returns>
        public static FixedSizeArray<T> Merge(FixedSizeArray<T> list1, FixedSizeArray<T> list2, bool listsSorted=false, IComparer<T> comparer=null)
        {
            int newCount = list1.GetCount() + list2.GetCount();
            FixedSizeArray<T> result = new FixedSizeArray<T>(newCount, comparer);
            if (listsSorted)
            {
                int index1 = 0;
                int index2 = 0;
                T t1 = list1.Get(index1);
                T t2 = list2.Get(index2);
                while (t1 != null || t2 != null)
                {
                    int comareResult;
                    if (t1 == null)
                    {
                        //add item from list2
                        comareResult = 1;
                    }
                    else if (t2 == null)
                    {
                        //add item from list1
                        comareResult = -1;
                    }
                    else
                    {
                        //add whichever one is 
                        comareResult = comparer.Compare(t1, t2);
                    }

                    if (comareResult < 0)
                    {
                        result.Add(t1);
                        index1++;
                        t1 = list1.GetSafe(index1);
                    }
                    else if (comareResult > 0)
                    {
                        result.Add(t2);
                        index2++;
                        t2 = list2.GetSafe(index2);
                    }
                    else
                    {
                        result.Add(t1);
                        index1++;
                        t1 = list1.GetSafe(index1);

                        result.Add(t2);
                        index2++;
                        t2 = list2.GetSafe(index2);
                    }

                }
            }
            else
            {
                foreach (T t in list1)
                {
                    if (t != null)
                        result.Add(t);
                }
                foreach (T t in list2)
                {
                    if (t != null)
                        result.Add(t);
                }
            }
            return result;
        }

        /// <summary>
        /// Inserts a new object into the array. If the array is full, an assert is thrown and the
        /// object is ignored.
        /// </summary>
        /// <param name="objct">Object to be added</param>
        public void Add(T objct)
        {
            Debug.Assert(mCount < mContents.Length, "Array exhausted! (" + mCount + ")");
            if (mCount < mContents.Length)
            {
                mContents[mCount] = objct;
                mSorted = false;
                mCount++;
            }
        }

        /// <summary>
        /// Inserts a new object into the array.  If the object is already in the array it is not added
        /// If the array is full, an assert is thrown and the object is ignored.
        /// </summary>
        /// <param name="objct">Object to try and add</param>
        /// <param name="ignoreComparator">Should the lookup ignore the comparator</param>
        public void AddDistinct(T objct, bool ignoreComparator)
        {
            if (Find(objct, ignoreComparator) == -1)
            {
                //not found
                Add(objct);
            }
        }

        /// <summary>
        /// Inserts an array of new objects into the array.  If the array is full, an assert is thrown and the
        /// remaining objects are ignored.
        /// </summary>
        /// <param name="array">Array to add</param>
        public void AddArray(FixedSizeArray<T> array)
        {
            foreach (T t in array)
            {
                Add(t);
            }
        }

        /// <summary>
        /// Inserts an array of new objects into the array.  If the array is full, an assert is thrown and the
        /// remaining objects are ignored.
        /// </summary>
        /// <param name="array">Array to add</param>
        /// <param name="ignoreComparator">Should this add ignore comparitor</param>
        public void AddDistinctArray(FixedSizeArray<T> array, bool ignoreComparator)
        {
            foreach (T t in array)
            {
                AddDistinct(t, ignoreComparator);
            }
        }
        
        /// <summary>
        /// Searches for an object and removes it from the array if it is found.  Other indexes in the
        /// array are shifted up to fill the space left by the removed object.  Note that if
        /// ignoreComparator is set to true, a linear search of object references will be performed.
        /// Otherwise, the comparator set on this array (if any) will be used to find the object.
        /// </summary>
        /// <param name="objct">Object to locate and remove</param>
        /// <param name="ignoreComparator">Use Comparator?</param>
        /// <returns>Object that was removed</returns>
        public T Remove(T objct, bool ignoreComparator)
        {
            T result = default(T);
            int index = Find(objct, ignoreComparator);
            if (index != -1)
            {
                result = Remove(index);
            }
            return result;
        }

        /// <summary>
        /// Searches for an object and removes it from the array if it is found.  Other indexes in the
        /// array are shifted up to fill the space left by the removed object.
        /// </summary>
        /// <param name="objct">Object to locate and remove</param>
        /// <returns>Object that was removed</returns>
        public T Remove(int index)
        {
            Debug.Assert(index < mCount, "Element index out of range");
            T result = default(T);
            if (index < mCount)
            {
                result = mContents[index];
                for (int x = index; x < mCount; x++)
                {
                    if (x + 1 < mContents.Length && x + 1 < mCount)
                    {
                        mContents[x] = mContents[x + 1];
                    }
                    else
                    {
                        mContents[x] = default(T);
                    }
                }
                mCount--;
            }
            return result;
        }

        /// <summary>
        /// Removes the last element in the array and returns it.  This method is faster than calling
        /// remove(count -1);
        /// </summary>
        /// <returns>The contents of the last element in the array</returns>
        public T RemoveLast()
        {
            T result = default(T);
            if (mCount > 0)
            {
                result = mContents[mCount - 1];
                mContents[mCount - 1] = default(T);
                mCount--;
            }
            return result;
        }

        /// <summary>
        /// Oject at given index is moved to last in the array
        /// </summary>
        /// <param name="index">Index to swap with last</param>
        public void SwapWithLast(int index)
        {
            Swap(index, mCount - 1);
        }

        /// <summary>
        /// Swaps two objcts in the array. If one or both of the indexes are out of range nothing is swpped
        /// </summary>
        /// <param name="index1"></param>
        /// <param name="index2"></param>
        public void Swap(int index1, int index2)
        {
            if (mCount > 0 && index1 < mCount && index2 < mCount && index1 != index2)
            {
                T objct = mContents[index1];
                mContents[index1] = mContents[index2];
                mContents[index2] = objct;
                mSorted = false;
            }
        }

        /// <summary>
        /// Sets the value of a specific index in the array.  An object must have already been added to
        /// the array at that index for this command to complete.
        /// </summary>
        /// <param name="index">Index to place the object at</param>
        /// <param name="objct">Object to be inserted</param>
        public void Set(int index, T objct)
        {
            Debug.Assert(index < mCount, "Element index out of range");
            if (index < mCount)
            {
                mContents[index] = objct;
            }
        }

        /// <summary>
        /// Clears the contents of the array, releasing all references to objects it contains and 
        /// setting its count to zero.
        /// </summary>
        public void Clear()
        {
            for (int x = 0; x < mCount; x++)
            {
                mContents[x] = default(T);
            }
            mCount = 0;
            mSorted = false;
        }

        /// <summary>
        /// Returns an entry from the array at the specified index. Will throw exception if out of range
        /// </summary>
        /// <param name="index">Index to return</param>
        /// <returns>Item at given index</returns>
        public T Get(int index)
        {
            Debug.Assert(index < mCount, "Element index out of range");
            T result = default(T);
            if (index < mCount && index >= 0)
            {
                result = mContents[index];
            }
            return result;
        }

        /// <summary>
        /// Returns an entry from the array at the specified index
        /// </summary>
        /// <param name="index">Index to return</param>
        /// <returns>Item at given index. default if nothing is found</returns>
        public T GetSafe(int index)
        {
            if (index >= mCount)
                return default(T);
            return Get(index);
        }

        /// <summary>
        /// Returns a random element in the array
        /// </summary>
        /// <returns>random element in the array</returns>
        public T GetRandom()
        {
            return Get(FreshMath.Random.Next(0, mCount));
        }
        
        /// <summary>
        /// Returns the raw internal array.  Exposed here so that tight loops can cache this array
        /// and walk it without the overhead of repeated function calls.  Beware that changing this array
        /// can leave FixedSizeArray in an undefined state, so this function is potentially dangerous
        /// and should be used in read-only cases.
        /// </summary>
        /// <returns>he internal storage array</returns>
        public T[] GetArray()
        {
            return mContents;
        }

        /// <summary>
        /// Searches the array for the specified object.  If the array has been sorted with sort(),
        /// and if no other order-changing events have occurred since the sort (e.g. add()), a
        /// binary search will be performed.  If a comparator has been specified with setComparator(),
        /// it will be used to perform the search.  If not, the default comparator for the object type
        /// will be used.  If the array is unsorted, a linear search is performed.
        /// Note that if ignoreComparator is set to true, a linear search of object references will be 
        /// performed. Otherwise, the comparator set on this array (if any) will be used to find the
        /// object.
        /// </summary>
        /// <param name="objct">The object to search for</param>
        /// <param name="ignoreComparator">Use comparator?</param>
        /// <returns>The index of the object in the array, or -1 if the object is not found.</returns>
        public int Find(T objct, bool ignoreComparator)
        {
            int index = -1;
            int count = mCount;
            bool sorted = mSorted;
            IComparer<T> comparator = mComparator;
            T[] contents = mContents;
            if (sorted && !ignoreComparator && count > LINEAR_SEARCH_CUTOFF)
            {
                if (comparator != null)
                {
                    index = Array.BinarySearch(contents, objct, comparator);
                }
                else
                {
                    index = Array.BinarySearch(contents, objct);
                }
                // Arrays.binarySearch() returns a negative insertion index if the object isn't found,
                // but we just want a boolean.
                if (index < 0)
                {
                    index = -1;
                }
            }
            else
            {
                // unsorted, linear search

                if (comparator != null && !ignoreComparator)
                {
                    for (int x = 0; x < count; x++)
                    {
                        int result = comparator.Compare(contents[x], objct);
                        if (result == 0)
                        {
                            index = x;
                            break;
                        }
                        else if (result > 0 && sorted)
                        {
                            // we've passed the object, early out
                            break;
                        }
                    }
                }
                else
                {
                    for (int x = 0; x < count; x++)
                    {
                        try
                        {
                            if ((object)contents[x] == (object)objct)
                            {
                                index = x;
                                break;
                            }
                        }
                        catch (Exception)
                        {
                            //
                            Debug.Assert(false, "This type is incompatable with a find without a comparer. Can continue without find");
                            break;
                        }
                    }
                }
            }
            return index;
        }

        /// <summary>
        /// Sorts the array.  If the array is already sorted, no work will be performed unless
        /// the forceResort parameter is set to true.  If a comparator has been specified with
        /// setComparator(), it will be used for the sort; otherwise the object's natural ordering will
        /// be used.
        /// </summary>
        /// <param name="forceResort">If set to true, the array will be resorted even if the order of the
        /// objects in the array has not changed since the last sort.</param>
        public void Sort(bool forceResort)
        {
            if (!mSorted || forceResort)
            {
                if (mComparator != null)
                {
                    mSorter.Sort(mContents, mCount, mComparator);
                }
                else
                {
                    //DebugLog.d(Titans.LOG_TAG, "No comparator specified for this type, using Arrays.sort().");

                    Array.Sort(mContents, 0, mCount);
                }
                mSorted = true;
            }
        }

        /// <summary>
        /// Returns the number of objects in the array.
        /// </summary>
        /// <returns>Count of elements in the array</returns>
        public int GetCount()
        {
            return mCount;
        }

        /// <summary>
        /// Returns the maximum number of objects that can be inserted inot this array.
        /// </summary>
        /// <returns>Max suze if array</returns>
        public int GetCapacity()
        {
            return mContents.Length;
        }

        /// <summary>
        /// Sets a comparator to use for sorting and searching.
        /// </summary>
        /// <param name="comparator">Comparator to use</param>
        public void SetComparator(IComparer<T> comparator)
        {
            mComparator = comparator;
            mSorted = false;
        }

        public void SetSorter(Sorter<T> sorter)
        {
            mSorter = sorter;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return (IEnumerator)GetEnumerator();
        }

        public ArrayEnum<T> GetEnumerator()
        {
            return new ArrayEnum<T>(mContents, mCount);
        }

        public override string ToString()
        {
            return "FixedSizeArray<" + typeof(T).Name + ">(" + Count + " of " + Capacity + ")";
        }
    }

    public class ArrayEnum<T> : IEnumerator
    {
        public T[] mContents;
        private int count;

        // Enumerators are positioned before the first element until the first MoveNext() call.
        int position = -1;

        public ArrayEnum(T[] list, int count)
        {
            mContents = list;
            this.count = count;
        }

        public bool MoveNext()
        {
            position++;
            return (position < count);
        }

        public void Reset()
        {
            position = -1;
        }

        object IEnumerator.Current
        {
            get
            {
                return Current;
            }
        }

        public T Current
        {
            get
            {
                try
                {
                    return mContents[position];
                }
                catch (IndexOutOfRangeException)
                {
                    throw new InvalidOperationException();
                }
            }
        }
    }

    public abstract class Sorter<T>
    {
        public abstract void Sort(T[] array, int count, IComparer<T> comparator);
    }

    public class StandardSorter<T> : Sorter<T>
    {


        public override void Sort(T[] array, int count, IComparer<T> comparator)
        {
            Array.Sort(array, 0, count, comparator);
        }

    }

    public class ShellSorter<T> : Sorter<T>
    {
        /** 
         * Shell sort implementation based on the one found here:
         * http://www.augustana.ab.ca/~mohrj/courses/2004.winter/csc310/source/ShellSort.java.html
         * Note that the running time can be tuned by adjusting the size of the increment used
         * to pass over the array each time.  Currently this function uses Robert Cruse's suggestion
         * of increment = increment / 3 + 1.
         */
        public override void Sort(T[] array, int count, IComparer<T> comparator)
        {
            int increment = count / 3 + 1;

            // Sort by insertion sort at diminishing increments.
            while (increment > 1)
            {
                for (int start = 0; start < increment; start++)
                {
                    InsertionSort(array, count, start, increment, comparator);
                }
                increment = increment / 3 + 1;
            }

            // Do a final pass with an increment of 1.
            // (This has to be outside the previous loop because the formula above for calculating the
            // next increment will keep generating 1 repeatedly.)
            InsertionSort(array, count, 0, 1, comparator);
        }

        
        /// <summary>
        /// Insertion sort modified to sort elements at a
        /// fixed increment apart.
        ///
        /// The code can be revised to eliminate the initial
        /// 'if', but I found that it made the sort slower.
        /// </summary>
        /// <param name="array">array to sort</param>
        /// <param name="count">end index of sort</param>
        /// <param name="start">start pos</param>
        /// <param name="increment">increment</param>
        /// <param name="comparator"></param>
        public void InsertionSort(T[] array, int count, int start, int increment,
                IComparer<T> comparator)
        {
            int j;
            int k;
            T temp;

            for (int i = start + increment; i < count; i += increment)
            {
                j = i;
                k = j - increment;
                int delta = comparator.Compare(array[j], array[k]);

                if (delta < 0)
                {
                    // Shift all previous entries down by the current
                    // increment until the proper place is found.
                    temp = array[j];
                    do
                    {
                        array[j] = array[k];
                        j = k;
                        k = j - increment;
                    } while (j != start && comparator.Compare(array[k], temp) > 0);
                    array[j] = temp;
                }
            }
        }
    }
}