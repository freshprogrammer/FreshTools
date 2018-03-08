using System;
using System.Collections.Generic;
using System.IO;

namespace FreshTools
{
    //Load should preserve this files path so a sibling file can use relative pathing

    public class VariablesFile
    {
        public static readonly string SCRIPT_LINE_BREAK = "" + (char)13 + (char)10;
        public const string SCRIPT_COMMENT = "//";
        public const string SCRIPT_COMMAND_LOAD = "LOAD";
        public const string SCRIPT_COMMAND_VARIABLE = "VAR";
        public const string SCRIPT_COMMAND_END = "END";

        private List<ScriptLine> lines;
        public VariableLibrary variables;
        protected IScriptReader parent;
        
        public VariablesFile(IScriptReader parent, bool caseSensetiveVaraibleNames)
        {
            this.parent = parent;
            lines = new List<ScriptLine>();
            variables = new VariableLibrary(caseSensetiveVaraibleNames);
        }

        public VariablesFile(string fileName, IScriptReader parent, bool caseSensetiveVaraibleNames)
            : this(parent, caseSensetiveVaraibleNames)
        {
            LoadFile(fileName);
        }

        public void LoadFile(string fileName)
        {
            Profiler.Start();
            StreamReader streamReader = null;
            if(File.Exists(fileName))
                streamReader = new StreamReader(fileName);
			//BufferedReader stream = null;// = new BufferedReader(new Reader(Reader(MyMethods.ShowOpenWindow(panel);
            if (streamReader == null)
            {
                //throw new Error("Invalid file name - \"" + fileName + "\"");
            }
            else
            {
                string line = streamReader.ReadLine();

                while (line != null)//Read from file
                {
                    //add to lines
                    ScriptLine sl = new ScriptLine(line, this, parent);
                    lines.Add(sl);

                    if (line.Trim().ToUpper().Equals(SCRIPT_COMMAND_END))
                    {
                        break;
                    }

                    //process Variables
                    if (sl.Variable != null)
                        variables.Add(sl.Variable);

                    //get next line
                    line = streamReader.ReadLine();
                }
                streamReader.Close();
            }
            Profiler.Stop();
        }

        public void RenameVariable(string oldName, string newName)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                try
                {
                    string varName = lines[i].Content;
                    if (varName == null || varName.Trim().Length == 0 || varName.Substring(0, 2) == "//")
                        continue;
                    varName = varName.Replace("var ", "").Trim();
                    varName = varName.Substring(0, varName.IndexOf("=")).Trim();
                    StringComparison sc = StringComparison.CurrentCulture;
                    if (variables.CaseSensitive)
                        sc = StringComparison.CurrentCultureIgnoreCase;

                    if (varName.Equals(oldName, sc))
                    {
                        variables.RemoveVariable(oldName);

                        if (newName != null)
                        {
                            //TODO not sure this should properly recursively scan through other script files.
                            lines[i] = new ScriptLine("var " + newName + " = " + lines[i].Content.Substring(lines[i].Content.IndexOf("=")+1), this, parent);
                            variables.Add(lines[i].Variable);
                        }
                        else//remove variable
                            lines.RemoveAt(i--);
                        break;
                    }
                }
                catch (ArgumentOutOfRangeException) { }
            }
        }

        public void RemoveVariable(string name)
        {
            RenameVariable(name, null);
        }

        //preserve comments and such by preserving all original text... not done
        public string SaveString()
        {
            List<Variable> includedVars = new List<Variable>();
            string result = "";
            foreach(ScriptLine sl in lines)
            {
                if (result.Length != 0)
                {
                    result += SCRIPT_LINE_BREAK;
                }
                result += sl.SaveString();

                if (sl.Variable != null)
                {
                    includedVars.Add(sl.Variable);
                }
            }
            //append extra variables to end of file
            foreach (Variable var in variables)
            {
                if (includedVars.Contains(var))
                {
                    //remove from temp list to speed up future look ups
                    includedVars.Remove(var);//this is unneccisary since this temp list is never used again
                    //skip this variable
                }
                else
                {
                    if (result.Length != 0)
                    {
                        result += SCRIPT_LINE_BREAK;
                    }
                    result += var.SaveString();
                }
            }
            return result;
        }

        public void SaveAs(string fileName)
        {
            System.IO.File.WriteAllText(fileName, SaveString());
        }
    }
    class ScriptLine
    {
        private LineType Type { get; set; }
        public Variable Variable{get;set;}
        public string Content;

        enum LineType
        {
            Variable,
            Command,
            Comment
        }

        public ScriptLine(string line, VariablesFile script, IScriptReader parentScriptReader)
        {
            Content = line;
            Type = LineType.Comment;//untill proven wrong
            if (line.Trim().Length == 0 || line.Trim().Substring(0, 2).Equals(VariablesFile.SCRIPT_COMMENT))
		    	{
		    		//comment or blank - ignore
		    	}
		    	else
		    	{
                    if (line.Substring(0, 3).ToUpper().Equals(VariablesFile.SCRIPT_COMMAND_VARIABLE))
		    		{
		    			//variable definition
		    			line = line.Substring(3).Trim(); // 
                        string varName = line.Substring(0, line.IndexOf("=")).Trim();
                        string varValue = line.Substring(line.IndexOf("=") + 1).Trim();
		    			Variable = new Variable(varName,varValue);

                        Type = LineType.Variable;//untill proven wrong
                    }
                    else if (line.Substring(0, 4).ToUpper().Equals(VariablesFile.SCRIPT_COMMAND_LOAD))
                    {
                        line = line.Substring(4).Trim();
                        //should preserver this files path so a sibling file can use relative pathing
                        script.LoadFile(line);

                        Type = LineType.Command;//untill proven wrong
                    }
                    else
                    {
                        //continue to write code for cmds - script language - borring!!!!
                        if (parentScriptReader != null)
                            parentScriptReader.ParseScriptLine(line);
                    }
		    	}
        }

        public string SaveString()
        {
            switch(Type)
            {
                default:
                case LineType.Comment:
                case LineType.Command:
                    //return the original content 
                    return Content;
                case LineType.Variable:
                    return Variable.SaveString();
            }
        }
    }
    public interface IScriptReader
    {
        void ParseScriptLine(string line);
    }
}
