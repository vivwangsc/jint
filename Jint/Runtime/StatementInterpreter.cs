﻿using System;
using System.Collections.Generic;
using System.Linq;
using Jint.Native;
using Jint.Parser.Ast;
using Jint.Runtime.Environments;
using Jint.Runtime.References;

namespace Jint.Runtime
{
    public class StatementInterpreter
    {
        private readonly Engine _engine;

        public StatementInterpreter(Engine engine)
        {
            _engine = engine;
        }

        private Completion ExecuteStatement(Statement statement)
        {
            return _engine.ExecuteStatement(statement);
        }

        public Completion ExecuteEmptyStatement(EmptyStatement emptyStatement)
        {
            return new Completion(Completion.Normal, null, null);
        }

        public Completion ExecuteExpressionStatement(ExpressionStatement expressionStatement)
        {
            var exprRef = _engine.EvaluateExpression(expressionStatement.Expression);
            return new Completion(Completion.Normal, _engine.GetValue(exprRef), null);
        }

        public Completion ExecuteIfStatement(IfStatement ifStatement)
        {
            var exprRef = _engine.EvaluateExpression(ifStatement.Test);
            Completion result;

            if (TypeConverter.ToBoolean(_engine.GetValue(exprRef)))
            {
                result = ExecuteStatement(ifStatement.Consequent);
            }
            else if (ifStatement.Alternate != null)
            {
                result = ExecuteStatement(ifStatement.Alternate);
            }
            else
            {
                return new Completion(Completion.Normal, null, null);
            }

            return result;
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.6.1
        /// </summary>
        /// <param name="doWhileStatement"></param>
        /// <returns></returns>
        public Completion ExecuteDoWhileStatement(DoWhileStatement doWhileStatement)
        {
            object v = null;
            bool iterating;

            do
            {
                var stmt = ExecuteStatement(doWhileStatement.Body);
                if (stmt.Value != null)
                {
                    v = stmt.Value;
                }
                if (stmt.Type != Completion.Continue /* todo: || stmt.Target*/)
                {
                    if (stmt.Type == Completion.Break /* todo: complete */)
                    {
                        return new Completion(Completion.Normal, v, null);
                    }

                    if (stmt.Type != Completion.Normal)
                    {
                        return stmt;
                    }
                }
                var exprRef = _engine.EvaluateExpression(doWhileStatement.Test);
                iterating = TypeConverter.ToBoolean(_engine.GetValue(exprRef));

            } while (iterating);

            return new Completion(Completion.Normal, v, null);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.6.2
        /// </summary>
        /// <param name="whileStatement"></param>
        /// <returns></returns>
        public Completion ExecuteWhileStatement(WhileStatement whileStatement)
        {
            object v = null; 
            while (true)
            {
                var exprRef = _engine.EvaluateExpression(whileStatement.Test);

                if (!TypeConverter.ToBoolean(_engine.GetValue(exprRef)))
                {
                    return new Completion(Completion.Normal, v, null);
                }

                var stmt = ExecuteStatement(whileStatement.Body);

                if (stmt.Value != null)
                {
                    v = stmt.Value;
                }

                if (stmt.Type != Completion.Continue /* todo: complete */)
                {
                    if (stmt.Type == Completion.Break /* todo: complete */)
                    {
                        return new Completion(Completion.Normal, v, null);
                    }

                    if (stmt.Type != Completion.Normal)
                    {
                        return stmt;
                    }
                }
            }
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.6.3
        /// </summary>
        /// <param name="forStatement"></param>
        /// <returns></returns>
        public Completion ExecuteForStatement(ForStatement forStatement)
        {
            
            if (forStatement.Init != null)
            {
                if (forStatement.Init.Type == SyntaxNodes.VariableDeclaration)
                {
                    ExecuteStatement(forStatement.Init.As<Statement>());
                }
                else
                {
                    _engine.GetValue(_engine.EvaluateExpression(forStatement.Init.As<Expression>()));
                }
            }

            object v = null;
            while (true)
            {
                if (forStatement.Test != null)
                {
                    var testExprRef = _engine.EvaluateExpression(forStatement.Test);
                    if (!TypeConverter.ToBoolean(_engine.GetValue(testExprRef)))
                    {
                        return new Completion(Completion.Normal, v, null);
                    }
                }

                var stmt = ExecuteStatement(forStatement.Body);
                if (stmt.Value != null)
                {
                    v = stmt.Value;
                }
                if (stmt.Type == Completion.Break /* todo: complete */)
                {
                    return new Completion(Completion.Normal, v, null);
                }
                if (stmt.Type != Completion.Continue /* todo: complete */)
                {
                    if (stmt.Type != Completion.Normal)
                    {
                        return stmt;
                    }
                }
                if (forStatement.Update != null)
                {
                    var incExprRef = _engine.EvaluateExpression(forStatement.Update);
                    _engine.GetValue(incExprRef);
                }
            }
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.6.4
        /// </summary>
        /// <param name="forInStatement"></param>
        /// <returns></returns>
        public Completion ExecuteForInStatement(ForInStatement forInStatement)
        {
            Identifier identifier = forInStatement.Left.Type == SyntaxNodes.VariableDeclaration 
                                        ? forInStatement.Left.As<VariableDeclaration>().Declarations.First().Id 
                                        : forInStatement.Left.As<Identifier>();

            var varRef = _engine.EvaluateExpression(identifier) as Reference;
            var exprRef = _engine.EvaluateExpression(forInStatement.Right);
            var experValue = _engine.GetValue(exprRef);
            if (experValue == Undefined.Instance || experValue == Null.Instance)
            {
                return new Completion(Completion.Normal, null, null);
            }


            var obj = TypeConverter.ToObject(_engine, experValue);
            object v = null;
            var keys = obj.Properties.Keys.ToArray();
            foreach (var p in keys)
            {
                var value = obj.Properties[p];
                if (!value.EnumerableIsSet)
                {
                    continue;
                }

                _engine.PutValue(varRef, p);

                var stmt = ExecuteStatement(forInStatement.Body);
                if (stmt.Value != null)
                {
                    v = stmt.Value;
                }
                if (stmt.Type == Completion.Break /* todo: complete */)
                {
                    return new Completion(Completion.Normal, v, null);
                }
                if (stmt.Type != Completion.Continue /* todo: complete */)
                {
                    if (stmt.Type != Completion.Normal)
                    {
                        return stmt;
                    }
                }

            }

            return new Completion(Completion.Normal, v, null);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.7
        /// </summary>
        /// <param name="continueStatement"></param>
        /// <returns></returns>
        public Completion ExecuteContinueStatement(ContinueStatement continueStatement)
        {
            return new Completion(Completion.Continue, null, continueStatement.Label != null ? continueStatement.Label.Name : null);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.8
        /// </summary>
        /// <param name="breakStatement"></param>
        /// <returns></returns>
        public Completion ExecuteBreakStatement(BreakStatement breakStatement)
        {
            return new Completion(Completion.Break, null, breakStatement.Label != null ? breakStatement.Label.Name : null);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.9
        /// </summary>
        /// <param name="statement"></param>
        /// <returns></returns>
        public Completion ExecuteReturnStatement(ReturnStatement statement)
        {
            if (statement.Argument == null)
            {
                return new Completion(Completion.Return, Undefined.Instance, null);
            }
            
            var exprRef = _engine.EvaluateExpression(statement.Argument);    
            return new Completion(Completion.Return, _engine.GetValue(exprRef), null);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.10
        /// </summary>
        /// <param name="withStatement"></param>
        /// <returns></returns>
        public Completion ExecuteWithStatement(WithStatement withStatement)
        {
            var val = _engine.EvaluateExpression(withStatement.Object);
            var obj = TypeConverter.ToObject(_engine, _engine.GetValue(val));
            var oldEnv = _engine.ExecutionContext.LexicalEnvironment;
            var newEnv = LexicalEnvironment.NewObjectEnvironment(_engine, obj, oldEnv, true);
            _engine.ExecutionContext.LexicalEnvironment = newEnv;

            Completion c;
            try
            {
                c = ExecuteStatement(withStatement.Body);
            }
            catch (JavaScriptException e)
            {
                c = new Completion(Completion.Throw, e.Error, null);
            }
            finally
            {
                _engine.ExecutionContext.LexicalEnvironment = oldEnv;
            }

            return c;
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.11
        /// </summary>
        /// <param name="switchStatement"></param>
        /// <returns></returns>
        public Completion ExecuteSwitchStatement(SwitchStatement switchStatement)
        {
            var exprRef = _engine.EvaluateExpression(switchStatement.Discriminant);
            var r = ExecuteSwitchBlock(switchStatement.Cases, _engine.GetValue(exprRef));
            if (r.Type == Completion.Break /* too: complete */)
            {
                return new Completion(Completion.Normal, r.Value, null);
            }
            return r;
        }

        public Completion ExecuteSwitchBlock(IEnumerable<SwitchCase> switchBlock, object input)
        {
            object v = null;
            SwitchCase defaultCase = null;
            foreach (var clause in switchBlock)
            {
                if (clause.Test == null)
                {
                    defaultCase = clause;
                }
                else
                {
                    var clauseSelector = _engine.GetValue(_engine.EvaluateExpression(clause.Test));
                    if (ExpressionInterpreter.StriclyEqual(clauseSelector, input))
                    {
                        if (clause.Consequent != null)
                        {
                            var r = ExecuteStatementList(clause.Consequent);
                            if (r.Type != Completion.Normal)
                            {
                                return r;
                            }
                            v = r.Value;
                        }
                    }
                }
            }

            if (defaultCase != null)
            {
                var r = ExecuteStatementList(defaultCase.Consequent);
                if (r.Type != Completion.Normal)
                {
                    return r;
                }
                v = r.Value;
            }

            return new Completion(Completion.Normal, v, null);
        }

        public Completion ExecuteStatementList(IEnumerable<Statement> statementList)
        {
            var c = new Completion(Completion.Normal, Undefined.Instance, null);
            Completion sl = c;

            try
            {
                foreach (var statement in statementList)
                {
                    c = ExecuteStatement(statement);
                    if (c.Type != Completion.Normal)
                    {
                        return new Completion(c.Type, c.Value ?? sl.Value, c.Identifier);
                    }

                    sl = c;
                }
            }
            catch(JavaScriptException v)
            {
                return new Completion(Completion.Throw, v.Error, null);
            }

            return new Completion(c.Type, c.Value ?? sl.Value, c.Identifier);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.13
        /// </summary>
        /// <param name="throwStatement"></param>
        /// <returns></returns>
        public Completion ExecuteThrowStatement(ThrowStatement throwStatement)
        {
            var exprRef = _engine.EvaluateExpression(throwStatement.Argument);
            return new Completion(Completion.Throw, _engine.GetValue(exprRef), null);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.14
        /// </summary>
        /// <param name="tryStatement"></param>
        /// <returns></returns>
        public Completion ExecuteTryStatement(TryStatement tryStatement)
        {
            var b = ExecuteStatement(tryStatement.Block);
            if (b.Type == Completion.Throw)
            {
                // execute catch
                if (tryStatement.Handlers.Any())
                {
                    foreach (var catchClause in tryStatement.Handlers)
                    {
                        var c = _engine.GetValue(b);
                        var oldEnv = _engine.ExecutionContext.LexicalEnvironment;
                        var catchEnv = LexicalEnvironment.NewDeclarativeEnvironment(_engine, oldEnv);
                        catchEnv.Record.CreateMutableBinding(catchClause.Param.Name);
                        catchEnv.Record.SetMutableBinding(catchClause.Param.Name, c, false);
                        _engine.ExecutionContext.LexicalEnvironment = catchEnv;
                        b = ExecuteStatement(catchClause.Body);
                        _engine.ExecutionContext.LexicalEnvironment = oldEnv;
                    }
                }
            }

            if (tryStatement.Finalizer != null)
            {
                var f = ExecuteStatement(tryStatement.Finalizer);
                if (f.Type == Completion.Normal)
                {
                    return b;
                }
            
                return f;
            }

            return b;
        }

        public Completion ExecuteProgram(Program program)
        {
            _engine.FunctionDeclarationBindings(program, _engine.ExecutionContext.LexicalEnvironment, true, program.Strict);
            _engine.VariableDeclarationBinding(program.VariableDeclarations, _engine.ExecutionContext.LexicalEnvironment.Record, true, program.Strict);

            return ExecuteStatementList(program.Body);
        }

        public Completion ExecuteVariableDeclaration(VariableDeclaration statement)
        {
            string lastIdentifier = null;
            foreach (var declaration in statement.Declarations)
            {
                if (declaration.Init != null)
                {
                    var lhs = _engine.EvaluateExpression(declaration.Id) as Reference;

                    if (lhs == null)
                    {
                        throw new ArgumentException();
                    }

                    lastIdentifier = lhs.GetReferencedName();
                    var value = _engine.GetValue(_engine.EvaluateExpression(declaration.Init));
                    _engine.PutValue(lhs, value);
                }
            }

            return new Completion(Completion.Normal, lastIdentifier, null);
        }

        public Completion ExecuteBlockStatement(BlockStatement blockStatement)
        {
            return ExecuteStatementList(blockStatement.Body);
        }

        public Completion ExecuteDebuggerStatement(DebuggerStatement debuggerStatement)
        {
            if (!System.Diagnostics.Debugger.IsAttached)
            {
                System.Diagnostics.Debugger.Launch();
            }
            
            System.Diagnostics.Debugger.Break();

            return new Completion(Completion.Normal, null, null);
        }
    }
}
