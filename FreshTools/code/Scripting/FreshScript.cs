using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace FreshTools
{

    //requires semi colin

    //Load should preserve this files path so a sibling file can use relative pathing

    public class FreshScript
    {
        private readonly string LINE_BREAK = "" + (char)13 + (char)10;

        private const bool caseSensitive = true;

        private List<FreshScriptFunction> functions;
        private List<string> lines;
        public VariableLibrary localVariables;
        private IScriptReader parent;

        private VariableLibrary activeVariableLibrary;

        private enum Keywords
        {
            comment,
            var,
            function,
            import,
            openBrace,
            closeBrace
        }

        public FreshScript(string fileName, IScriptReader parent)
        {
            this.parent = parent;
            lines = new List<string>();
            functions = new List<FreshScriptFunction>();
            localVariables = new VariableLibrary(caseSensitive);
            activeVariableLibrary = localVariables;


            //test code

            string testLine = "var tezt = \"Game 1 (testing)\"  && (yeah && test);";
            string variableName, operation, valueString;
            if (ParseVariableLineForFirstOperation(testLine, out variableName, out operation, out  valueString))
            {

            }

            //int x =0;
            //int y = 1;
            //y+ +;

            //end test code


            lines = LoadFile(fileName, 0);
            
            OutputLinesForDebug(fileName + ".txt");
            ProcessBaseLines();
        }

        private List<string> LoadFile(string fileName, int loadRecursionDepth)
        {
            //working here - make this load only - imports only - move function definisitons to another pass/method initialize - dont add loads to line List
            const int recursiveLoadMax = 10;

            List<string> loadLines = new List<string>();

            Profiler.Start();
            StreamReader streamReader = null;
            if (File.Exists(fileName))
                streamReader = new StreamReader(fileName);
            //BufferedReader stream = null;// = new BufferedReader(new Reader(Reader(MyMethods.ShowOpenWindow(panel);
            if (streamReader == null)
            {
                ReportError("Failed to Load/Read file \""+fileName+"\".");
            }
            else
            {
                string fileText = streamReader.ReadToEnd();
                if (fileText != null)
                {
                    fileText = fileText.Trim();
                    string fileTextWithoutQuotes = GetLineWithoutQuotes(fileText);
                    string codeLine = "not null";
                    while (codeLine!=null)
                    {
                        codeLine = NextCodeLineFromInput(ref fileText, ref fileTextWithoutQuotes);
                        if(codeLine!=null)
                            loadLines.Add(codeLine);
                    }
                    if (fileText.Trim().Length > 0)
                    {
                        ReportError("There is loose code at the end of the file. Probably missing semi colin or brace.",fileName,-1,"");
                    }
                }
                streamReader.Close();




                int braceDepth = 0;
                bool funtionStart = false;
                bool inFunctionDefinition = false;

                FreshScriptFunction activeFunction = null;
                                        
                int lineNo = -1;
                //interpret lines - predefine functions mostly
                foreach (string line in loadLines)
                {
                    lineNo++;

                    string remainingLine;
                    Keywords keywordMatch;

                    if (HasLeadingKeyword(line, out remainingLine, out keywordMatch))
                    {
                        switch (keywordMatch)
                        {
                            case Keywords.comment:
                                //do nothing
                                break;
                            case Keywords.var:
                                //this will be handled in the process line
                                break;
                            case Keywords.import:
                                {
                                    //should preserver this files path so a sibling file can use relative pathing
                                    if (loadRecursionDepth < recursiveLoadMax)
                                    {
                                        List<string> importLines = LoadFile(remainingLine, loadRecursionDepth + 1);
                                    }
                                    else
                                    {
                                        //XXX test this
                                        ReportError("Attempting to import file past inport depth limit. Possibly a recursive Load problem. If this is intentional contact Fresh.", fileName, lineNo, loadLines[lineNo - 1]);
                                    }
                                    break;
                                }
                            case Keywords.function:
                                {
                                    if (inFunctionDefinition)
                                    {
                                        ReportError("Already defining a function", fileName, lineNo, loadLines[lineNo - 1]);
                                    }
                                    else
                                    {
                                        funtionStart = true;
                                        //create a dummy function definition that will get redefined when the end of funciton is found
                                        int paramaterCount;
                                        string functionName;
                                        if (ProcessFuncionDefinition(remainingLine, out functionName, out paramaterCount))
                                        {
                                            activeFunction = new FreshScriptFunction(functionName, paramaterCount, lineNo + 1, 0, caseSensitive);
                                        }
                                    }
                                    break;
                                }
                            case Keywords.openBrace:
                                if (funtionStart)
                                {

                                    if (braceDepth != 0)
                                    {
                                        ReportError("Cannot start a function definition within another code block", fileName, lineNo, loadLines[lineNo - 1]);
                                    }

                                    funtionStart = false;
                                    activeFunction.StartLine = lineNo + 1;
                                    inFunctionDefinition = true;
                                    braceDepth++;
                                }
                                break;
                            case Keywords.closeBrace:
                                braceDepth--;

                                if (braceDepth < 0)
                                {
                                    ReportError("Attempting to close non existant block", fileName, lineNo, loadLines[lineNo - 1]);
                                }
                                else
                                {
                                    if (braceDepth == 0 & inFunctionDefinition)
                                    {
                                        activeFunction.EndLine = lineNo - 1;

                                        if (activeFunction.LineCount <= 0)
                                        {
                                            ReportError("Warning: function " + activeFunction.Name + " has invalid line count(" + activeFunction.LineCount + ")", fileName, lineNo, loadLines[lineNo - 1]);
                                        }

                                        functions.Add(activeFunction);
                                    }
                                }


                                funtionStart = false;
                                break;
                        }
                    }
                }

                if (braceDepth >0)
                {
                    ReportError("Not all braces are closed. - Final load check", fileName, -1, "");
                }
            }
            Profiler.Stop();
            return loadLines;
        }

        private static void ReportError(string message, string fileName, int lineNo, string previousLine)
        {
            ReportError(message + " - file(" + fileName + ") line(" + lineNo + ") - after line \"" + previousLine + "\"");
        }

        private static void ReportError(string message, int lineNo)
        {
            ReportError(message + " - line(" + lineNo + ")");
        }

        private static void ReportError(string message)
        {
            //XXX finish this stub method with some sort of error reporting - on screen or log file or something...
            MethodBase mb = new StackTrace().GetFrame(1).GetMethod();
            string methodName = mb.DeclaringType + "." + mb.Name;

            message += " - " + methodName;
        }

        private string NextCodeLineFromInput(ref string input, ref string inputWithoutQuotes)
        {
            //XXX account for single and multi line comments - included comments as single line in lines
            //hmmm should be using string index values instead of recreating the string every pass
            string codeLine = null;

            bool regularLine = false;
            bool braceOnly = false;

            bool hitFirstCommentBrace = false;
            bool inSingleLineComment = false;
            bool inMultiLineComment = false;
            int endIndex = -1;
            for (int xx = 0; xx < input.Length; xx++)
            {
                if (inSingleLineComment || inMultiLineComment)
                    break;

                char c = inputWithoutQuotes[xx];
                bool foundEnd = false;
                switch (c)
                {
                    default:
                        continue;
                    case '/':
                        if(!hitFirstCommentBrace)
                        {
                            hitFirstCommentBrace = true;
                        }
                        else
                        {
                            hitFirstCommentBrace = false;
                            inSingleLineComment = true;
                        }
                        continue;
                    case '*':
                        if (hitFirstCommentBrace)
                        {
                            inMultiLineComment = true;
                        }
                        foundEnd = true;
                        continue;

                    case ';':
                        regularLine = true;
                        foundEnd = true;
                        break;
                    case '{':
                    case '}':
                        foundEnd = true;
                        break;
                }

                if (foundEnd)
                {
                    endIndex = xx;
                    braceOnly = xx == 0;
                    break;
                }
            }
             
            if (inSingleLineComment)
            {
                int lineBreakIndex = inputWithoutQuotes.IndexOf('\n');
                if (lineBreakIndex > 0)
                    endIndex = lineBreakIndex;
                else
                    endIndex = inputWithoutQuotes.Length;//end use end of file
            }
            else if (inMultiLineComment)
            {
                int endCommentIndex = inputWithoutQuotes.IndexOf("*/");
                if (endCommentIndex > 0)
                    endIndex = endCommentIndex+2;
                else
                    endIndex = inputWithoutQuotes.Length;//end use end of file
            }

            if (endIndex != -1)
            {
                int semiColinSize = 0;
                int codeLineLen = endIndex;

                if (regularLine)
                {
                    semiColinSize = 1;
                }

                if (braceOnly)
                {
                    codeLineLen = 1;
                }

                codeLine = input.Substring(0, codeLineLen).TrimEnd();//remove space between code and semi colin

                input = input.Substring(codeLineLen + semiColinSize).TrimStart();
                inputWithoutQuotes = inputWithoutQuotes.Substring(codeLineLen + semiColinSize).TrimStart();
            }

            return codeLine;
        }

        private void OutputLinesForDebug(string fileName)
        {
            string output = "";

            int lineNo = 0;
            foreach (string line in lines)
            {
                lineNo++;

                //dont output line wraps - replace them with spaces - treated as whitespace in code
                string thisLine = line.Replace("\r", " ");
                thisLine = thisLine.Replace("\n", " ");

                output += lineNo + "\t" + thisLine + "\n";
            }

            System.IO.File.WriteAllText(fileName, output);
        }

        private void ProcessBaseLines()
        {
            string remainingLine;
            Keywords keywordMatch;

            //0 = no
            //1 = yes
            //2 = just ended
            int inFunction = 0;

            foreach (string line in lines)
            {
                if (HasLeadingKeyword(line, out remainingLine, out keywordMatch))
                {
                    switch (keywordMatch)
                    {
                        case Keywords.comment:
                            //do nothing - short chircut processing of this line here - ignore comments
                            continue;
                        case Keywords.function:
                            inFunction = 1;
                            break;
                        //case Keywords.endfunc:
                        //    inFunction = 2;
                        //    break;
                    }
                }


                //process all lines that aren't  part of a function
                //variable definition - process if not in function - those will be process on the function call
                if (inFunction==0)
                {
                    ProcessLine(line);
                }

                if (inFunction == 2)
                    inFunction = 0;
            }
        }

        private void ProcessLine(string line)
        {
            //keyword test
            string remainingLine;
            Keywords keywordMatch;

            //function test
            List<string> functionParamaters;
            FreshScriptFunction functionMatch;

            //variable assignment test
            Variable assigningVariable;
            Variable assigningVariableValue;

            if (HasLeadingKeyword(line, out remainingLine, out keywordMatch))
            {
                switch (keywordMatch)
                {
                    case Keywords.comment:
                        //do nothing
                        break;
                    case Keywords.var:
                        //variable definition
                        ProcessVariableDefinition(remainingLine, activeVariableLibrary);
                        break;
                }
            }
            else if (IsFunctionCall(line, out functionParamaters, out functionMatch))
            {
                ProcessCodeBlock(functionMatch);
            }
            else if (IsVariableAssignment(line, out assigningVariable, out assigningVariableValue))
            {
                //ProcessCodeBlock(functionMatch);
            }
            else
            {
                //continue to write code for cmds - script language
                if (parent != null)
                    parent.ParseScriptLine(line);
            }
        }

        private void ProcessCodeBlock(FreshScriptCodeBlock codeBlock)
        {
            VariableLibrary preVariables = activeVariableLibrary;

            //set current variable library to the function's library
            if (codeBlock is FreshScriptFunction)
            {
                //activeVariableLibrary = ((FreshScriptFunction)codeBlock).Variables;

                //XXX add variable library stack so code can acces variable defined it 1 up scope
                //TODO optomize this - should have a variable librabry pool - and variable pool
                activeVariableLibrary = new VariableLibrary(caseSensitive);
            }

            for (int lineNo = codeBlock.StartLine; lineNo <= codeBlock.EndLine; lineNo++)
            {
                string line = lines[lineNo];
                ProcessLine(line);
            }

            activeVariableLibrary = preVariables;
        }

        /// <summary>
        /// takes in the function signature without the function keyword - returns  false if the signiture is invalid else returns the function name and the number of paramaters
        /// </summary>
        /// <param name="line">line without the function keyword</param>
        /// <param name="functionName">function name</param>
        /// <param name="paramaterCount">number of paramaters</param>
        /// <returns></returns>
        private static bool ProcessFuncionDefinition(string line, out string functionName, out int paramaterCount)
        {
            bool valid = false;
            functionName = null;
            paramaterCount = 0;

            int indexOfOpenParens = line.IndexOf("(");
            if (indexOfOpenParens > 0)
            {
                functionName = line.Substring(0, indexOfOpenParens);
                functionName = functionName.Trim();
                valid = true;
            }
            return valid;
        }

        private void ProcessVariableDefinition(string line, VariableLibrary variableLibrary)
        {
            string variableName, operation, value;
            if (ParseVariableLineForFirstOperation(line, out variableName, out operation, out  value))
            {
                if (operation == "=")
                {
                    //create variable - this also checks for existing variable by same name
                    Variable v = variableLibrary.CreateVariable(variableName);

                    if (operation == "=")
                    {
                        Variable calculatedValue = CalculateValue(value);
                        //set value
                        variableLibrary.SetValue(variableName, calculatedValue.GetValueSaveString());
                    }
                }
            }
        }

        /// <summary>
        /// Real workhorse of the complex variable logic. Converts variable names to values and does the math and returns final result. If value conversion fails an empty string is returned.
        /// </summary>
        /// <param name="original">string to be parsed into a value</param>
        /// <returns>value in the form of a Variable</returns>
        private Variable CalculateValue(string original)
        {
            const string valueVaraibleName = "VAL";
            //dummy value record - name is irelevant
            Variable result = new Variable(valueVaraibleName, original);
            original = original.Trim();

            //XXX nbkjlfdsnjkvfjkvbnxcjhdfvndhsbvkjcxzbvndfzjvbkjbdzvjkbxzvbhdbszkvjdfz   
            /*int index;         
            if(FreshArchives.Contains(original,operatorCharacters,out index))
            {
                //this value has an oporator in it
                //extract left value, right value and operator
                string leftString, operation, rightString;
                if (ParseVariableLine(original, out leftString, out operation, out  rightString))
                {
                    if (operation != "=")
                    {
                        Variable leftVal = CalculateValue(leftString);
                        Variable rightVal = CalculateValue(rightString);


                        if (Variable.IsNumber(leftVal) && Variable.IsNumber(rightVal))
                        {
                            //do math
                            switch (operation)
                            {
                                case "+":
                                    result.SetValue((leftVal.Double + rightVal.Double).ToString());
                                    break;
                                case "-":
                                    result.SetValue((leftVal.Double - rightVal.Double).ToString());
                                    break;
                                case "*":
                                    result.SetValue((leftVal.Double * rightVal.Double).ToString());
                                    break;
                                case "/":
                                    result.SetValue((leftVal.Double / rightVal.Double).ToString());
                                    break;
                                case "%":
                                    result.SetValue((leftVal.Double % rightVal.Double).ToString());
                                    break;
                                case "==":
                                    result.SetValue((leftVal.Double == rightVal.Double).ToString());
                                    break;
                                case "!=":
                                    result.SetValue((leftVal.Double != rightVal.Double).ToString());
                                    break;
                            }
                        }
                        else if (Variable.IsBoolean(leftVal) && Variable.IsBoolean(rightVal))
                        {
                            //do math
                            switch (operation)
                            {
                                case "==":
                                    result.SetValue((leftVal.Boolean == rightVal.Boolean).ToString());
                                    break;
                                case "!=":
                                    result.SetValue((leftVal.Boolean != rightVal.Boolean).ToString());
                                    break;
                                case "&&":
                                    result.SetValue((leftVal.Boolean && rightVal.Boolean).ToString());
                                    break;
                                case "||":
                                    result.SetValue((leftVal.Boolean || rightVal.Boolean).ToString());
                                    break;
                            }
                        }

                        //if (operation == "++" || operation == "--")
                        //{
                        //    string tempRightVal = "1";
                            
                        //}
                    }
                    else
                    {
                        //cannot handle asignments within an assignment
                        return new Variable(valueVaraibleName, ""); ;
                    }
                }
                else
                {
                    //failed to parse base arguments - return failure
                    return new Variable(valueVaraibleName, ""); ;
                }
            }
            else
            {
                //no operator - simple value extraction
                if (Variable.DetermineType(original) == Variable.VariableType.Unknown)
                {
                    //look up as variable name
                    Variable var = GetVariable(original);
                    if (var != null)
                    {
                        result.SetValue(var.GetValueSaveString());
                    }
                    else
                    {

                        //return value as it is
                    }
                }
                else
                {

                    //return value as it is
                }
            }*/
            return result;
        }

        private Variable GetVariable(string name)
        {
            //XXX make this spin backwards through variable libraries
            Variable result = activeVariableLibrary.FindVariable(name);
            if(result==null)
            {
                result = localVariables.FindVariable(name);
            }
            return result;
        }

        private readonly string[] operators = { "++", "--", "!", "*", "/", "%", "+", "-", "==", "!=", "&&", "||", "=", "*=", "/=", "+=", "-=", "%=" };

        /// <summary>
        /// Spin though characters in line, accounting for quotes, and finds the position for the 
        /// </summary>
        /// <param name="line"></param>
        /// <param name="leftSide">line on left side of operation</param>
        /// <param name="operation">operatoin</param>
        /// <param name="rightSide">line on right side of operation</param>
        /// <returns></returns>
        private bool ParseVariableLineForFirstOperation(string line, out string leftSide, out string operation, out string rightSide)
        {
            //this order http://bmanolov.free.fr/javaoperators.php

            bool valid = false;
            leftSide = null;
            operation = null;
            rightSide = null;

            string lineWithoutStrings = GetLineWithoutQuotes(line);

            int leftParamIndex = lineWithoutStrings.LastIndexOf('(');
            if (leftParamIndex >= 0)
            {
                //has paramaters - inner most
                int rightParamIndex = lineWithoutStrings.IndexOf(')',leftParamIndex);
                if (rightParamIndex >= 0)
                {
                    int innerLength = rightParamIndex - (leftParamIndex + 1);
                    int operationIndex = -1;
                    int operatorNo;
                    for (operatorNo = 0; operatorNo < operators.Length && operationIndex < 0; operatorNo++)
                    {
                        operationIndex = lineWithoutStrings.IndexOf(operators[operatorNo], leftParamIndex+1, innerLength);
                    }

                    if (operationIndex >= 0)
                    {
                        //valid set of paramaters - process this first

                    }
                }
            }

            return valid;
        }

        private static string GetLineWithoutQuotes(string line)
        {
            string swapInChar = '`'.ToString();
            string lineWithoutStrings = line;

        CheckForOpenQuotes:
            int leftQuoteIndex = lineWithoutStrings.IndexOf('\"');
            if (leftQuoteIndex >= 0)
            {
                int closeSearchStart = leftQuoteIndex + 1;
            CheckForCloseQuotes:
                int rightQuoteIndex = lineWithoutStrings.IndexOf('\"', closeSearchStart);
                if (rightQuoteIndex >= 0 && lineWithoutStrings[rightQuoteIndex - 1] != '\\')
                {
                    for (int charNo = leftQuoteIndex; charNo <= rightQuoteIndex; charNo++)
                    {
                        //spin through string replacing string contents with another char to prevent it interfering with other processing
                        lineWithoutStrings = lineWithoutStrings.Remove(charNo, 1);
                        lineWithoutStrings = lineWithoutStrings.Insert(charNo, swapInChar);
                    }
                    goto CheckForOpenQuotes;
                }
                else
                {
                    //invalid close quotes
                    closeSearchStart = rightQuoteIndex + 1;
                    goto CheckForCloseQuotes;
                }
            }
            return lineWithoutStrings;
        }

        private bool IsVariableAssignment(string line, out Variable assingingVaraible, out Variable value)
        {

            //was working on this and above method...
            string[] assignmentOperators = { "++", "--", "=", "*=", "/=", "+=", "-=", "%=" };
            bool valid = false;
            assingingVaraible = null;
            value = null;

            string leftSide, operation, rightSide;

            int operationIndex = -1;
            for (int operatorNo = 0; operatorNo < assignmentOperators.Length && operationIndex < 0; operatorNo++)
            {
                operationIndex = line.IndexOf(assignmentOperators[operatorNo]);
            }

            if (operationIndex > 0)
            {
                leftSide = line.Substring(0, operationIndex).Trim();

                int operatorLength = 1;
                int valueStartIndex = operationIndex + 1;
                if (Array.IndexOf(assignmentOperators, line[valueStartIndex]) > 0)
                {
                    valueStartIndex++;
                    operatorLength++;
                }

                operation = line.Substring(operationIndex, operatorLength).Trim();
                rightSide = line.Substring(operationIndex + operatorLength).Trim();
                valid = true;
            }
            return valid;
        }

        private bool IsFunctionCall(string line, out List<string> paramaters, out FreshScriptFunction functionMatch)
        {
            functionMatch = null;
            paramaters = null;

            string functionName = null;
            int paramaterCount = 0;
            if (ProcessFuncionDefinition(line, out functionName, out paramaterCount))
            {
                foreach (FreshScriptFunction f in functions)
                {
                    if (String.Compare(f.Name, functionName, !caseSensitive) == 0)
                    {
                        functionMatch = f;
                        if (paramaters == null)
                        {
                            //TODO add paramaters to this code
                            //first function with this name - define paramaters

                            if (true)
                            {
                                //if number of paramaters matches - same funciton signatues
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        private bool HasLeadingKeyword(string line, out string remainingLine, out Keywords keywordMatch)
        {
            keywordMatch = Keywords.comment;
            remainingLine = null;
            string[] keywords = Enum.GetNames(typeof(Keywords));

            if (line==null || line.Trim().Length==0 ||  HasLeadingKeyword(line, "//", out remainingLine))
            {
                keywordMatch = Keywords.comment;
                return true;
            }

            if (line == "{")
            {
                keywordMatch = Keywords.openBrace;
                return true;
            }
            if (line == "}")
            {
                keywordMatch = Keywords.closeBrace;
                return true;
            }

            foreach (String keyword in keywords)
            {
                if (HasLeadingKeyword(line, keyword, out remainingLine))
                {
                    keywordMatch = (Keywords)Enum.Parse(typeof(Keywords), keyword);
                    return true;
                }
            }
            return false;
        }

        private bool HasLeadingKeyword(string line, string testKeyword, out string remainingLine)
        {
            remainingLine = null;
            bool match = false;
            if (line.Length >= testKeyword.Length)
            {
                if (String.Compare(line.Substring(0, testKeyword.Length),testKeyword,!caseSensitive)==0)
                {
                    match = true;
                    remainingLine = line.Substring(testKeyword.Length).Trim();
                }
            }
            return match;
        }
    }
    class FreshScriptCodeBlock
    {
        public int StartLine = 0;
        public int EndLine = 0;

        public int LineCount { get { return EndLine - StartLine; } }

        public FreshScriptCodeBlock(int start, int end)
        {
            StartLine = start;
            EndLine = end;
        }
    }
    class FreshScriptFunction : FreshScriptCodeBlock
    {
        private String name;
        private int paramaterCount;
        private VariableLibrary variables;

        public String Name { get { return name; } }
        public int ParamaterCount { get { return paramaterCount; } }
        public VariableLibrary Variables { get { return variables; } }

        public FreshScriptFunction(string name, int paramaterCount, int start, int end, bool caseSensitiveVaraibles)
            : base(start, end)
        {
            this.name = name;
            this.paramaterCount = paramaterCount;
            variables = new VariableLibrary(caseSensitiveVaraibles);
        }

        public override string ToString()
        {
            string paramaters = "";
            for (int xx = 0; xx < paramaterCount; xx++)
            {
                if (xx != 0)
                {
                    paramaters += ", ";
                }
                paramaters += "param#"+xx;
            }
            return Name + "(" + paramaters + ")";
        }
    }
}