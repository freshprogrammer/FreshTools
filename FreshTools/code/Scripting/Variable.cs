using System;
using System.Collections.Generic;

namespace FreshTools
{
    public class Variable
    {
        public enum VariableType
        {
            Unknown,
            Boolean,
            Int,
            Float,
            Double,
            String
        }

        private string name;
        private string value = "";
        private VariableType type;

        public static VariableType DetermineType(object val)
        {
            VariableType result = 0;
            Type t = val.GetType();

            if (t == (true).GetType())
            {
                result = VariableType.Boolean;
            }
            else if (t == (5).GetType())
            {
                result = VariableType.Int;
            }
            else if (t == (5f).GetType())
            {
                result = VariableType.Float;
            }
            else if (t == (5.0d).GetType())
            {
                result = VariableType.Double;
            }
            else if (t == ("").GetType())
            {
                result = VariableType.String;
            }
            else
            {
                result = VariableType.Unknown;
            }

            return result;
        }

        public static bool IsNumber(Variable var)
        {
            return IsNumber(var.type);
        }

        public static bool IsNumber(VariableType type)
        {
            switch (type)
            {
                case VariableType.Double:
                case VariableType.Float:
                case VariableType.Int:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsBoolean(Variable var)
        {
            switch (var.type)
            {
                case VariableType.Boolean:
                    return true;
                case VariableType.Int:
                    int val = var.Int;
                    if (val == 0 || val == 1)
                        return true;
                    else
                        return false;
                default:
                    return false;
            }
        }

        public static bool CanCast(VariableType origin, VariableType dest)
        {
            if (dest == VariableType.String)
                return true;

            if (IsNumber(origin) && IsNumber(dest))
                return true;

            return false;
        }

        public static VariableType DetermineType(string val)
        {
            VariableType result = 0;

            if (String.Compare(val.Trim(), "true", true) == 0 || String.Compare(val.Trim(), "false", true) == 0)
            {
                result = VariableType.Boolean;
            }
            else
            {
                try
                {
                    if (val.Trim().Equals("" + int.Parse(val)))
                    {
                        result = VariableType.Int;
                    }
                }
                catch (FormatException)
                {
                    try
                    {
                        if (val.Trim().Equals("" + float.Parse(val)))
                        {
                            result = VariableType.Float;
                        }
                    }
                    catch (FormatException)
                    {
                        try
                        {
                            if (val.Trim().Equals("" + double.Parse(val)))
                            {
                                result = VariableType.Double;
                            }
                        }
                        catch (FormatException)
                        {
                            string trimmed = val.Trim();
                            if (trimmed.Length>0 && trimmed[0] == '\"' && trimmed[trimmed.Length - 1] == '\"')
                            {
                                result = VariableType.String;
                            }
                            else
                            {
                                result = VariableType.Unknown;
                            }
                        }
                    }

                }
            }

            return result;
        }

        public Variable(string name, string defaultValue)
        {
            this.name = name.Trim();
            value = defaultValue.Trim();
            type = DetermineType(value);

            if (type == VariableType.String)
            {
                value = value.Substring(1, value.Length - 2);
            }

        }

        public Variable(string name, object linkedVariable)
        {
            this.name = name.Trim();
            value = linkedVariable.ToString();
            type = DetermineType(value); ;

            if (type == VariableType.String)
            {
                value = value.Substring(1, value.Length - 2);
            }
        }

        public string GetName()
        {
            return name;
        }

        public VariableType GetVariableType()
        {
            return type;
        }

        public void SetValue(string val)
        {
            value = val;
            type = DetermineType(value);
        }

        public object GetValueAsObject()
        {
            switch (type)
            {
                case VariableType.Boolean:
                    return Boolean;
                case VariableType.Int:
                    return Int;
                case VariableType.Float:
                    return Float;
                case VariableType.Double:
                    return Double;
                case VariableType.String:
                case VariableType.Unknown:
                default:
                    return value;
            }
        }

        public bool Boolean 
        { 
            get 
            {
                if(type==VariableType.Int)
                {
                    int val = Int;
                    if (val == 1)
                        return true;
                    else if (val == 0)
                        return false;
                }
                return Boolean.Parse(value);
            } 
        }
        public int Int { get { return int.Parse(value); } }
        public float Float { get { return float.Parse(value); } }
        public double Double { get { return double.Parse(value); } }
        public string String { get { return value; } }
        public string StringWithQuotes { get { return "\"" + value + "\""; } }

        public override string ToString()
        {
            return "Variable{" + name + "," + value + "," + type + "}";
        }

        public string GetValueSaveString()
        {
            string valueSS = value;

            if (type == VariableType.Boolean)
            {
                //correct "False" to "false"
                valueSS = valueSS.ToLower();
            }
            else if (type == VariableType.String)
            {
                valueSS = StringWithQuotes;
            }
            return valueSS;
        }

        public string saveString()
        {
            return "var " + name + " = " + GetValueSaveString();
        }

    }
    class VariablesComparator : IComparer<Variable>
    {
        private bool caseSensitive;
        public VariablesComparator(bool caseSensitive)
        {
            this.caseSensitive = caseSensitive;
        }

        public int Compare(Variable object1, Variable object2)
        {
            return String.Compare(object1.GetName(),object2.GetName(),!caseSensitive);
        }
    }
}
