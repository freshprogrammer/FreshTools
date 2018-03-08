using System;
using System.Collections;
using System.Collections.Generic;

namespace FreshTools
{
    public class VariableLibrary : IEnumerable
    {
        public readonly string LINE_BREAK = "" + (char)13 + (char)10;

	    private List<Variable> variables;
        private VariablesComparator variablesComparator;
        private bool variableListSorted = true;
        private Variable searchDummy;
        public readonly bool CaseSensitive;
	    
	    public VariableLibrary(bool caseSensitive)
        {
            this.CaseSensitive = caseSensitive;
            variablesComparator = new VariablesComparator(caseSensitive);
            variables = new List<Variable>();
	    }

        /// <summary>
        /// Add vaiable if no variable found with this name. If variable exists already do nothing.
        /// </summary>
        /// <param name="name">Name of variable to create</param>
        /// <returns>Created or existing variable</returns>
        public Variable CreateVariable(string name)
	    {
            Variable v = new Variable(name, "");
            return Add(v);
	    }

        /// <summary>
        /// Add vaiable if no variable found with this name. If variable exists already do nothing.
        /// </summary>
        /// <param name="name">Name of variable to create</param>
        /// <param name="defaultValue">default value</param>
        /// <returns>Created or existing variable</returns>
        public Variable CreateVariable(string name, object defaultValue)
	    {
            Variable v = new Variable(name, defaultValue);
            return Add(v);
	    }

        /// <summary>
        /// Add vaiable if no variable found with this name. If variable exists already do nothing.
        /// </summary>
        /// <param name="name">Name of variable to create</param>
        /// <param name="defaultValue">default value</param>
        /// <returns>Created or existing variable</returns>
        public Variable CreateVariable(string name, string defaultValue)
	    {
            Variable v = new Variable(name, defaultValue);
            return Add(v);
	    }

        /// <summary>
        /// Add vaiable if no variable found with this name. If variable exists already do nothing.
        /// </summary>
        /// <param name="newVar"></param>
        /// <returns>The new or existing variable in library</returns>
        public Variable Add(Variable newVar)
	    {
            Variable existing = FindVariable(newVar.GetName());
            if (existing!=null)
		    {
                //variable already exists - leave it alone
                return existing;
		    }
		    else
		    {
                variables.Add(newVar);
                variableListSorted = false;
                return newVar;
            }
	    }

        /// <summary>
        /// Remove the variable with the given name
        /// </summary>
        /// <param name="name">Name of the variable to remove</param>
        /// <returns>The variable removed or null is none is found</returns>
        public Variable RemoveVariable(String name)
        {
            Variable v = FindVariable(name);
            if (v != null)
                RemoveVariable(v);
            return v;
        }

        /// <summary>
        /// Remove the variable if its in the list
        /// </summary>
        /// <param name="var">Variable to be removed</param>
        public void RemoveVariable(Variable var)
        {
            variables.Remove(var);
        }

        /// <summary>
        /// Update the value of the variable with the given name
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
	    public void SetValue(string name, string value)
	    {
            Variable v = FindVariable(name);
		    if(v!=null)
                v.SetValue(value);
	    }

        /// <summary>
        /// Attempts to find variable with given name. Returns null if not found
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public Variable FindVariable(string name)
        {
            if (!variableListSorted)
            {
                variables.Sort(variablesComparator);
                variableListSorted = true;
            }

            searchDummy = new Variable(name,"");

            int index = variables.BinarySearch(searchDummy, variablesComparator);

            if (index < 0)
                return null;
            else
                return variables[index];
        }

        /// <summary>
        /// Returns the variable with the given name if found. If none is found, a new one is created and added to the list.
        /// </summary>
        /// <param name="name">Name of the Variable</param>
        /// <param name="defaultValue">Default value if no variable is found</param>
        /// <returns>The variable in the library with this name</returns>
        public Variable GetVariable(string name, object defaultValue)
        {
            bool found;
            return GetVariable(name, defaultValue, out found);
        }

        /// <summary>
        /// Returns the variable with the given name if found. If none is found, a new one is created and added to the list.
        /// </summary>
        /// <param name="name">Name of the Variable</param>
        /// <param name="defaultValue">Default value if no variable is found</param>
        /// <param name="found">True if found, False if created</param>
        /// <returns>The variable in the library with this name</returns>
        public Variable GetVariable(string name, object defaultValue, out bool found)
        {
            found = false;
            Variable result = FindVariable(name);

            if (result == null)
            {
                //variable doesnt exist yet - add it
                result = CreateVariable(name, defaultValue);
            }
            else
            {
                found = true;
            }
            return result;
        }

        /// <summary>
        /// Returns the variable with the given name if found. If none is found, a new one is created and added to the list.
        /// </summary>
        /// <param name="name">Name of the Variable</param>
        /// <param name="linkedVariable">Linked Variable that will be Default value of the variable if it doesnt exist</param>
        /// <param name="writeToLinkedVariable">If true, overwriteds the linked variable with the contents of the librairy variable (if found)</param>
        /// <returns>The variable in the library with this name</returns>
        public Variable GetVariable<T>(string name, ref T linkedVariable, bool writeToLinkedVariable)
        {
            bool found;
            Variable result = GetVariable(name, linkedVariable, out found);
            if (writeToLinkedVariable && found)
            {
                try
                {
                    object resultValue = result.GetValueAsObject();
                    linkedVariable = (T)resultValue;
                }
                catch (InvalidCastException)
                {
                    //the variable types dont match  - change type to correct this
                    result.SetValue(linkedVariable.ToString());
                }
            }
            return result;
        }

        /// <summary>
        /// Returns a new list of variable names in this library
        /// </summary>
        /// <returns></returns>
	    public List<string> GetVariableNames()
	    {
            List<string> result = new List<string>(variables.Count);
            foreach (Variable v in variables)
            {
			    result.Add(v.GetName());
		    }
		    return result;
	    }
	    
        /// <summary>
        /// Generates a text dump to save all variable in this library
        /// </summary>
        /// <returns></returns>
	    public string SaveString()
	    {
            string result = "";
            foreach (Variable v in variables)
            {
                result += v.SaveString() + LINE_BREAK;
		    }
            result.TrimEnd();
		    return result;
	    }

	    public void SaveAs(string fileName)
	    {
		    System.IO.File.WriteAllText(fileName, SaveString());
	    }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return (IEnumerator)GetEnumerator();
        }

        public ArrayEnum<Variable> GetEnumerator()
        {
            return new ArrayEnum<Variable>(variables.ToArray(), variables.Count);
        }
    }
}
