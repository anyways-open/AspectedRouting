using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using AspectedRouting.Language.Functions;
using AspectedRouting.Language.Typ;
using Type = AspectedRouting.Language.Typ.Type;

namespace AspectedRouting.Language
{
    public interface IExpression
    {
        IEnumerable<Type> Types { get; }

        /// <summary>
        ///     Evaluates the expression.
        ///     Gives null if the expression should not give a value
        /// </summary>
        /// <returns></returns>
        object Evaluate(Context c, params IExpression[] arguments);

        /// <summary>
        ///     Creates a copy of this expression, but only execution paths of the given types are kept
        ///     Return null if no execution paths are found
        /// </summary>
        IExpression Specialize(IEnumerable<Type> allowedTypes);

        IExpression PruneTypes(System.Func<Type, bool> allowedTypes);

        /// <summary>
        ///     Optimize a single expression, eventually recursively (e.g. a list can optimize all the contents)
        /// </summary>
        /// <returns></returns>
        IExpression Optimize(out bool somethingChanged);

        /// <summary>
        ///     Optimize with the given argument, e.g. listdot can become a list of applied arguments.
        /// </summary>
        /// <param name="argument"></param>
        /// <returns></returns>
        //  IExpression OptimizeWithArgument(IExpression argument);
        void Visit(Func<IExpression, bool> f);

        bool Equals(IExpression other);

        /**
         * Builds a string representation that can be used to paste into C# test programs
         */
        string Repr();


    }

    public static class ExpressionExtensions
    {

        public static void PrintRepr(this IExpression e)
        {
            Console.WriteLine(e.Repr() + "\n\n-----------\n");
        }
        public static object Run(this IExpression e, Context c, Dictionary<string, string> tags)
        {
            try
            {
                var result = e.Apply(new Constant(tags)).Evaluate(c);
                while (result is IExpression ex)
                {
                    result = ex.Apply(new Constant(tags)).Evaluate(c);
                }

                return result;
            }
            catch (Exception err)
            {
                throw new Exception($"While evaluating the expression {e} with arguments a list of tags", err);
            }
        }

        public static double EvaluateDouble(this IExpression e, string paramName, Context c, params IExpression[] arguments)
        {
            return Utils.AsDouble(e.Evaluate(c, arguments), paramName);
        }

        public static IExpression Specialize(this IExpression e, Type t)
        {
            if (t == null)
            {
                throw new NullReferenceException("Cannot specialize to null");
            }

            return e.Specialize(new[] { t });
        }

        public static IExpression Specialize(this IExpression e, Dictionary<string, Type> substitutions)
        {
            var newTypes = new HashSet<Type>();

            foreach (var oldType in e.Types)
            {
                var newType = oldType.Substitute(substitutions);
                if (newType == null)
                {
                    continue;
                }

                newTypes.Add(newType);
            }

            if (!newTypes.Any())
            {
                return null;
            }

            return e.Specialize(newTypes);
        }

        public static IEnumerable<Type> SpecializeToCommonTypes(this IEnumerable<IExpression> exprs,
            out IEnumerable<IExpression> specializedExpressions)
        {
            exprs.SpecializeToCommonTypes(out var types, out specializedExpressions);
            return types;
        }

        /// <summary>
        ///     Runs over all expressions, determines a common ground by unifications
        ///     Then specializes every expression onto this common ground
        /// </summary>
        /// <returns>The common ground of types</returns>
        [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
        public static IEnumerable<IExpression> SpecializeToCommonTypes(this IEnumerable<IExpression> exprs,
            out IEnumerable<Type> specializedTypes, out IEnumerable<IExpression> specializedExpressions)
        {
            specializedTypes = null;
            var allExpressions = new HashSet<IExpression>();
            specializedExpressions = allExpressions;

            foreach (var expr in exprs)
            {
                if (specializedTypes == null)
                {
                    specializedTypes = expr.Types; // This is t
                }
                else
                {
                    var newlySpecialized = Typs.WidestCommonTypes(specializedTypes, expr.Types);
                    if (!newlySpecialized.Any())
                    {
                        throw new ArgumentException("Could not find a common ground for types \n\t" + specializedTypes.Pretty() + " and \n\t" + expr.Types.Pretty()+"\n\tDid you perhaps forgot to add the `$` at your function invocation?");
                    }

                    specializedTypes = newlySpecialized;
                }


            }

            foreach (var expr in exprs)
            {
                var e = expr.Specialize(specializedTypes);
                allExpressions.Add(e);
            }

            return specializedExpressions = allExpressions;
        }
    }
}