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
            bool b;
            int i;
            float f;
            double d;

            val = val.Trim();

            if (val.Length <= 0)
                return VariableType.Unknown;
            else if (bool.TryParse(val, out b))
                return VariableType.Boolean;
            else if (int.TryParse(val, out i))
                return VariableType.Int;
            else if (float.TryParse(val, out f))
                return VariableType.Float;
            else if (double.TryParse(val, out d))
                return VariableType.Double;
            else if (val[0] == '\"' && val[val.Length - 1] == '\"')//start and end eith quotes
                return VariableType.String;

            return VariableType.Unknown;
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

        public string SaveString()
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
