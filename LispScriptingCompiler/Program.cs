using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;

namespace LispScriptingCompiler
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
        }
        static List<(string, ExpressionType)> binaryMethods = new List<(string, ExpressionType)> {
            ("==",ExpressionType.Equal),
            ("!=",ExpressionType.NotEqual),
            (">=",ExpressionType.GreaterThanOrEqual),
            ("<=",ExpressionType.LessThanOrEqual),
            (">",ExpressionType.GreaterThan),
            ("<",ExpressionType.LessThan),
            ("+",ExpressionType.Add),
            ("-",ExpressionType.Subtract),
            ("*",ExpressionType.Multiply),
            ("/",ExpressionType.Divide),
            ("%",ExpressionType.Modulo),
            ("??",ExpressionType.Coalesce)
        };

        static Expression BuildLISPTree(string input, List<(string, MethodInfo)> methods, List<(string, Expression)> expMethods,
            List<(string, Expression)> variables)
        {
            bool complete = false;
            TokenizeStrings(ref input, variables);
            while(!complete)
            {
                Expression tree = null;
                var start = input.LastIndexOf('(');
                if (start == -1)
                    break;
                var end = input.IndexOf(')', start);
                var innerMostExp = input.Substring(start + 1, end - start - 1);
                var expSplit = Regex.Split(innerMostExp, @"\s+");
                var op = expSplit[0];
                var lstArgs = new List<Expression>();
                foreach(var arg in expSplit.Skip(1))
                {
                    lstArgs.Add(EvalArg(arg, variables));
                }
                if (binaryMethods.Any(s => s.Item1 == op))
                {
                    var bType = binaryMethods.First(s => s.Item1 == op).Item2;
                    if (lstArgs[0].Type != lstArgs[1].Type)
                    {
                        var rankedList = new List<Type> { typeof(short), typeof(int), typeof(long), typeof(decimal), typeof(float) };
                        if (!rankedList.Contains(lstArgs[0].Type) && rankedList.Contains(lstArgs[1].Type))
                            throw new Exception("Type mismatch on binary");
                        var pArg1 = rankedList.IndexOf(lstArgs[0].Type);
                        var pArg2 = rankedList.IndexOf(lstArgs[1].Type);
                        if (pArg1 > pArg2)
                            lstArgs[1] = Expression.Convert(lstArgs[1], lstArgs[0].Type);
                        else
                            lstArgs[0] = Expression.Convert(lstArgs[0], lstArgs[1].Type);
                    }
                    tree = Expression.MakeBinary(bType, lstArgs[0], lstArgs[1]);
                }
                else if (op == "prop")
                    tree = Expression.PropertyOrField(lstArgs[0], ((ConstantExpression)lstArgs[1]).Value.ToString());
                else if (op == "idx")
                    tree = Expression.ArrayAccess(lstArgs[0], lstArgs.Skip(1).ToArray());
                else if (op == "list-add")
                    tree = Expression.Call(lstArgs[0], "Add", null, lstArgs[1]);
                else if (op == "if" || op == "if-else")
                {
                    LabelTarget lblRet = Expression.Label(lstArgs[1].Type);
                    Expression ifTrue = Expression.Return(lblRet, lstArgs[1]);
                    Expression defaultVal;
                    if (lstArgs[1].Type.IsValueType && lstArgs[1].Type != typeof(void))
                        defaultVal = Expression.Constant(Activator.CreateInstance(lstArgs[1].Type));
                    else
                        defaultVal = Expression.Constant(null);
                    if (op == "if")
                    {
                        if (lstArgs[1].Type != typeof(void))
                            tree = Expression.Block(Expression.IfThen(lstArgs[0], ifTrue), Expression.Label(lblRet, defaultVal));
                        else
                            tree = Expression.IfThen(lstArgs[0], lstArgs[1]);
                    }
                    else if (op == "if-else")
                    {
                        Expression ifFalse = Expression.Return(lblRet, lstArgs[2]);
                        if (lstArgs[1].Type != typeof(void))
                            tree = Expression.Block(Expression.IfThenElse(lstArgs[0], ifTrue, ifFalse), Expression.Label(lblRet, defaultVal));
                        else
                            tree = Expression.IfThenElse(lstArgs[0], lstArgs[1], lstArgs[2]);
                    }
                }
                else if (op == "block" || op =="begin")
                    tree = Expression.Block(lstArgs);
                else if (op =="define")
                {

                }
                else if (methods.Any(s=>s.Item1 == op))
                {
                    var cntParams = expSplit.Length - 1;
                    var mthdInfo = methods.First(s => s.Item1 == op && s.Item2.GetParameters().Length == cntParams).Item2;
                    if (mthdInfo.IsStatic)
                        tree = Expression.Call(null, mthdInfo, lstArgs.ToArray());
                    else
                        tree = Expression.Call(lstArgs[0], mthdInfo, lstArgs.Skip(1).ToArray());
                }
                var name = $"@Eval{variables.Count}";
                input = input.Remove(start, end - start + 1);
                input = input.Insert(start, name);
                variables.Add((name, tree));
            }
            return variables.Last().Item2;
        }

        static Expression EvalArg(string input, List<(string, Expression)> variables)
        {
            if (input.StartsWith("\"") && input.EndsWith("\""))
                return Expression.Constant(input.Substring(1, input.Length - 2));
            else if (input.StartsWith("'") && input.EndsWith("'") && input.Length == 3)
                return Expression.Constant(input[1]);
            else if (variables.Any(s => s.Item1 == input))
                return variables.First(s => s.Item1 == input).Item2;
            else if (Regex.IsMatch(input, @"\d+"))
                return Expression.Constant(int.Parse(input));
            else if (Regex.IsMatch(input, @"\d+\.\d+"))
                return Expression.Constant(decimal.Parse(input));
            else if (input == "true")
                return Expression.Constant(true);
            else if (input == "false")
                return Expression.Constant(false);
            else if (input == "null")
                return Expression.Constant(null);


            throw new Exception("arg not parsed");
        }

        static void TokenizeStrings(ref string input, List<(string,Expression)> variables)
        {
            //stack int and all that jazz, reference strings in input in variables
        }
    }
}
