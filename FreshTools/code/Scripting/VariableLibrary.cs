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
	
	    public VariableLibrary(bool caseSensitive)
        {
            variablesComparator = new VariablesComparator(caseSensitive);
            variables = new List<Variable>();
	    }

        public Variable CreateVariable(string name)
	    {
		    Variable v = new Variable(name,"");
		    Add(v);
            return v;
	    }

        public Variable CreateVariable(string name, object defaultValue)
	    {
            Variable v = new Variable(name, defaultValue);
            Add(v);
            return v;
	    }

        public Variable CreateVariable(string name, string defaultValue)
	    {
            Variable v = new Variable(name, defaultValue);
            Add(v);
            return v;
	    }

        public Variable Add(Variable newVar)
	    {
            Variable existing = FindVariable(newVar.GetName());
            if (existing!=null)
		    {
                //variable already exists - leave it alone
		    }
		    else
		    {
                variables.Add(newVar);
                variableListSorted = false;
            }
            return newVar;
	    }
	
	    public void SetValue(string name, string value)
	    {
            Variable v = FindVariable(name);
		    if(v!=null)
                v.SetValue(value);
	    }

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

        public Variable GetVariable(string name, object defaultValue)
        {
            bool found;
            return GetVariable(name, defaultValue, out found);
        }

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
        /// Returns the variable with the given name. If found, If none is found, a new one is created and added to the list.
        /// </summary>
        /// <param name="name">Name of the Variable</param>
        /// <param name="linkedVariable">Linked Variable that will be Default value of the variable if it doesnt exist</param>
        /// <param name="writeToLinkedVariable">If true, overwriteds the linked variable with the contents of the librairy variable (if found)</param>
        /// <returns></returns>
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
	
	    public List<string> GetVariableNames()
	    {
            List<string> result = new List<string>(variables.Count);//was Size +1 - not sure why
            foreach (Variable v in variables)
            {
			    result.Add(v.GetName());
		    }
		    return result;
	    }
	
	    public string SaveString()
	    {
            string result = "";
            foreach (Variable v in variables)
            {
			    if(result.Length!=0)
			    {
				    result += LINE_BREAK;
			    }
			    result += v.saveString();
		    }
		    return result;
	    }
	
	    public void SaveAs(string fileName)
	    {
		    System.IO.File.WriteAllText(fileName, SaveString());
	    }

        //XXX deprecate this method - now unsafe with sorting
        public List<Variable> GetVariables()
        {
            return variables;
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
