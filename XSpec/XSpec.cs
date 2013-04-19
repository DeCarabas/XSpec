using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Linq.Expressions;
using System.IO;

namespace Doty.Spec
{
    /// <summary>A small class that helps you write tests that are easier to read and understand.</summary>
    /// <example>
    /// <code>
    ///[TestMethod]
    ///public void WhenIncrementingAnInteger()
    ///{
    ///    int x = 0;
    ///    XSpec.Given( "an integer, set to zero", () => x = 0 )
    ///        .When( "the integer is incremented", () => x++ )
    ///            .It( "can be incremented again to two", () => { x++; Assert.AreEqual( x, 2 ); } )
    ///            .It( "should be 1", () => x == 1 )
    ///        .When( "the integer is incremented again", () => x++ )
    ///            .It( "should be 2", () => x == 2 )
    ///        .When( "the integer is divided by zero", () => x = x / ( x - 2 ) )
    ///            .ItShouldThrow&lt;DivideByZeroException>()
    ///    .Go();
    ///}        
    /// </code>
    /// </example>
    public class XSpec
    {
        readonly Action action;
        readonly List<XSpec> children = new List<XSpec>();
        readonly string description;
        Exception exception;
        readonly XSpec parent;
        readonly List<Tuple<long, string>> results = new List<Tuple<long, string>>();
        ExecState state;
        bool swallowExceptions;
        readonly NodeType type;

        XSpec( string description, Action action, XSpec parent, NodeType type )
        {
            this.action = action;
            this.description = description;
            this.parent = parent;
            this.type = type;
        }

        /// <summary>
        /// Adds a new node to the this spec. The new node may or may not be a child of this one.
        /// </summary>
        /// <param name="description">The description associated with the node.</param>
        /// <param name="action">The action associated with the node.</param>
        /// <param name="type">The type of the node.</param>
        /// <returns>The new node.</returns>
        XSpec AddNode( string description, Action action, NodeType type )
        {
            return AddNode( description, action, type, type );
        }

        /// <summary>
        /// Adds a new node to the this spec, with an explicit precedence override. The new node may or may not be a 
        /// child of this one.
        /// </summary>
        /// <param name="description">The description associated with the node.</param>
        /// <param name="action">The action associated with the node.</param>
        /// <param name="type">The type of the node.</param>
        /// <param name="precedence">The type to use as the precedence of this node. This parameter exists specifically
        /// so that 'TheException' and 'It' nodes can be peers in the tree, rather than one being the child of the 
        /// other.</param>
        /// <returns>The new node.</returns>
        XSpec AddNode( string description, Action action, NodeType type, NodeType precedence )
        {
            XSpec parent = this;
            while ( parent.type >= precedence )
            {
                parent = parent.parent;
            }

            var node = new XSpec( description, action, parent, type );
            parent.children.Add( node );
            return node;
        }

        bool Exec()
        {
            string message = null;

            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                this.action();
            }
            catch ( Exception e )
            {
                this.exception = e;
            }
            stopwatch.Stop();

            ExecState resultState;
            if ( this.exception != null && !this.swallowExceptions )
            {
                message = this.exception.Message;
                if ( this.exception is AssertFailedException || this.exception is AssertInconclusiveException )
                {
                    resultState = ExecState.Failed;
                }
                else
                {
                    resultState = ExecState.Exception;
                }
            }
            else
            {
                resultState = ExecState.Passed;
            }

            this.results.Add( Tuple.Create( stopwatch.ElapsedMilliseconds, message ) );
            if ( this.state <= resultState ) { this.state = resultState; }
            return ( resultState == ExecState.Passed || this.swallowExceptions );
        }

        /// <summary>
        /// Begins a new specification, by specifying the initial conditions. Execute the specification by calling 
        /// <see cref="Go"/>.
        /// </summary>
        /// <param name="description">A string describing the initial conditions.</param>
        /// <param name="action">The action to run to set up the initial conditions. This action should completely
        /// reset the state, regardless of what the state was before being executed.</param>
        /// <returns>A new specification, which will execute under the initial conditions.</returns>
        /// <remarks>The <see cref="description"/> parameter should be a short sentence that starts with 'Given ...', 
        /// but without the word 'Given'. The first reason for this is that the code should be legible. e.g., this:
        /// <code>
        ///   .Given( "a choice between dancing pigs or security", () => { choice = new Choice("dancing pigs", "security"); } )
        /// </code>
        /// <para>reads like "Given a choice between dancing pigs or security..."</para>
        /// <para>The second reason is that the string is printed out as part of the test results, and the word "Given"
        /// is prepended to the description.</para>
        /// <para>The provided delegate will be run many times; once for each assertion. It should completely reset
        /// the state of the world to the appropriate initial state, so that side effects caused by any given assertion
        /// are undone.</para></remarks>
        /// <example>
        /// <code>
        ///[TestMethod]
        ///public void WhenIncrementingAnInteger()
        ///{
        ///    int x = 0;
        ///    XSpec.Given( "an integer, set to zero", () => x = 0 )
        ///        .When( "the integer is incremented", () => x++ )
        ///            .It( "can be incremented again to two", () => { x++; Assert.AreEqual( x, 2 ); } )
        ///            .It( "should be 1", () => x == 1 )
        ///        .When( "the integer is incremented again", () => x++ )
        ///            .It( "should be 2", () => x == 2 )
        ///        .When( "the integer is divided by zero", () => x = x / ( x - 2 ) )
        ///            .ItShouldThrow&lt;DivideByZeroException>()
        ///    .Go();
        ///}        
        /// </code>
        /// </example>
        public static XSpec Given( string description, Action action )
        {
            return new XSpec( description, action, null, NodeType.Given );
        }

        /// <summary>
        /// Applies some action to the state described by the specification.
        /// </summary>
        /// <param name="description">A string describing the action that is taken. This should be a short sentence
        /// that starts with 'when ...', but eliding the word 'when'. For example, "the moon hits your eye like a big
        /// pizza pie" would be an acceptable value.</param>
        /// <param name="action">The action to take.</param>
        /// <returns>A new specification, which can be further enhanced with additional <see cref="When"/> or 
        /// <see cref="It"/> calls.</returns>
        /// <remarks>The <see cref="description"/> parameter should be a short sentence that starts with 'When ...', 
        /// but without the word 'When'. The first reason for this is that the code should be legible. e.g., this:
        /// <code>
        ///   .When( "you make a call to the service", () => service.Call() )
        /// </code>
        /// <para>reads like "When you make a call to the service..."</para>
        /// <para>The second reason is that the string is printed out as part of the test results, and the word "When"
        /// is prepended to the description.</para>
        /// <para>All calls to <c>When</c> are cumulative, that is, each call to <c>When</c> is done in the 
        /// context of the previous calls. To completely reset the state, you should start over again with another 
        /// spec, and another <c>Given</c> call.</para></remarks>
        /// <example>
        /// <code>
        ///[TestMethod]
        ///public void WhenIncrementingAnInteger()
        ///{
        ///    int x = 0;
        ///    XSpec.Given( "an integer, set to zero", () => x = 0 )
        ///        .When( "the integer is incremented", () => x++ )
        ///            .It( "can be incremented again to two", () => { x++; Assert.AreEqual( x, 2 ); } )
        ///            .It( "should be 1", () => x == 1 )
        ///        .When( "the integer is incremented again", () => x++ )
        ///            .It( "should be 2", () => x == 2 )
        ///        .When( "the integer is divided by zero", () => x = x / ( x - 2 ) )
        ///            .ItShouldThrow&lt;DivideByZeroException>()
        ///    .Go();
        ///}        
        /// </code>
        /// </example>
        public XSpec When( string description, Action action )
        {
            if ( String.IsNullOrEmpty( description ) ) { throw new ArgumentNullException( "description" ); }
            if ( action == null ) { throw new ArgumentNullException( "action" ); }

            return AddNode( description, action, NodeType.When );
        }

        /// <summary>
        /// Makes an assertion about the state of the world.
        /// </summary>
        /// <param name="description">A string describing the assertion being made. This should be a short sentence
        /// that starts with 'it ...', but eliding the 'it'. For example, "puts the lotion in the bucket" would be an
        /// acceptable value.</param>
        /// <param name="assertion">The assertion to be made. It should throw an <see cref="AssertionFailedException"/> 
        /// if it fails.</param>
        /// <returns>A new specification, which can have further actions taken in it.</returns>
        /// <remarks>The <see cref="description"/> parameter should be a short sentence that starts with 'It ...', 
        /// but without the word 'It'. The first reason for this is that the code should be legible. e.g., this:
        /// <code>
        ///   .It( "makes you think", () => Assert.IsTrue(self.Thinking) )
        /// </code>
        /// <para>reads like "It makes you think"</para>
        /// <para>The second reason is that the string is printed out as part of the test results, and the word "It"
        /// is prepended to the description.</para></remarks>
        /// <example>
        /// <code>
        ///[TestMethod]
        ///public void WhenIncrementingAnInteger()
        ///{
        ///    int x = 0;
        ///    XSpec.Given( "an integer, set to zero", () => x = 0 )
        ///        .When( "the integer is incremented", () => x++ )
        ///            .It( "can be incremented again to two", () => { x++; Assert.AreEqual( x, 2 ); } )
        ///            .It( "should be 1", () => x == 1 )
        ///        .When( "the integer is incremented again", () => x++ )
        ///            .It( "should be 2", () => x == 2 )
        ///        .When( "the integer is divided by zero", () => x = x / ( x - 2 ) )
        ///            .ItShouldThrow&lt;DivideByZeroException>()
        ///    .Go();
        ///}        
        /// </code>
        /// </example>
        public XSpec It( string description, Action assertion )
        {
            if ( description == null ) { throw new ArgumentNullException( "description" ); }
            if ( assertion == null ) { throw new ArgumentNullException( "action" ); }

            return AddNode( description, assertion, NodeType.It );
        }

        /// <summary>
        /// Makes an assertion about the state of the world.
        /// </summary>
        /// <param name="description">A string describing the assertion being made. This should be a short sentence
        /// that starts with 'it ...', but eliding the 'it'. For example, "puts the lotion in the bucket" would be an
        /// acceptable value.</param>
        /// <param name="assertion">The predicate to be evaluated. If it returns false, the assertion has failed.
        /// </param>
        /// <returns>A new specification, which can have further actions taken in it.</returns>
        /// <remarks>The <see cref="description"/> parameter should be a short sentence that starts with 'It ...', 
        /// but without the word 'It'. The first reason for this is that the code should be legible. e.g., this:
        /// <code>
        ///   .It( "makes you think", () => Assert.IsTrue(self.Thinking) )
        /// </code>
        /// <para>reads like "It makes you think"</para>
        /// <para>The second reason is that the string is printed out as part of the test results, and the word "It"
        /// is prepended to the description.</para>
        /// <para>The expression is analyzed, a little bit, in order to provide better assertion messages in the case
        /// of failure. In particular, if you write an '==' expression, the assertion is converted into an 
        /// Assert.Equals call, and the right-hand side of the '==' expression is considered the 'expected' value.
        /// </para></remarks>
        /// <example>
        /// <code>
        ///[TestMethod]
        ///public void WhenIncrementingAnInteger()
        ///{
        ///    int x = 0;
        ///    XSpec.Given( "an integer, set to zero", () => x = 0 )
        ///        .When( "the integer is incremented", () => x++ )
        ///            .It( "can be incremented again to two", () => { x++; Assert.AreEqual( x, 2 ); } )
        ///            .It( "should be 1", () => x == 1 )
        ///        .When( "the integer is incremented again", () => x++ )
        ///            .It( "should be 2", () => x == 2 )
        ///        .When( "the integer is divided by zero", () => x = x / ( x - 2 ) )
        ///            .ItShouldThrow&lt;DivideByZeroException>()
        ///    .Go();
        ///}        
        /// </code>
        /// </example>
        public XSpec It( string description, Expression<Func<bool>> assertion )
        {
            if ( description == null ) { throw new ArgumentNullException( "description" ); }
            if ( assertion == null ) { throw new ArgumentNullException( "predicate" ); }

            Action actualAction;
            if ( assertion.Body.NodeType == ExpressionType.Equal )
            {
                var be = (BinaryExpression)assertion.Body;
                var leftLambda = (Func<object>)Expression.Lambda( Expression.Convert( be.Left, typeof( object ) ) ).Compile();
                var rightLambda = (Func<object>)Expression.Lambda( Expression.Convert( be.Right, typeof( object ) ) ).Compile();
                actualAction = () => Assert.AreEqual( rightLambda(), leftLambda() );
            }
            else
            {
                var compiled = assertion.Compile();
                actualAction = () => Assert.IsTrue( compiled() );
            }

            return AddNode( description, actualAction, NodeType.It );
        }

        /// <summary>
        /// Asserts that the previous action should throw an exception of the specified type.
        /// </summary>
        /// <typeparam name="TException">The type of exception that should have been thrown.</typeparam>
        /// <returns>A spec under which additional actions and assertions can be made.</returns>
        /// <remarks>This assertion has the side effect of making the preceeding action no longer fail when an 
        /// exception is thrown; instead, the exception will be recorded and this assertion will make sure it was of
        /// the specified type.</remarks>
        /// <example>
        /// <code>
        ///[TestMethod]
        ///public void WhenIncrementingAnInteger()
        ///{
        ///    int x = 0;
        ///    XSpec.Given( "an integer, set to zero", () => x = 0 )
        ///        .When( "the integer is incremented", () => x++ )
        ///            .It( "can be incremented again to two", () => { x++; Assert.AreEqual( x, 2 ); } )
        ///            .It( "should be 1", () => x == 1 )
        ///        .When( "the integer is incremented again", () => x++ )
        ///            .It( "should be 2", () => x == 2 )
        ///        .When( "the integer is divided by zero", () => x = x / ( x - 2 ) )
        ///            .ItShouldThrow&lt;DivideByZeroException>()
        ///    .Go();
        ///}        
        /// </code>
        /// </example>
        public XSpec ItShouldThrow<TException>()
        {
            XSpec parent = null;

            XSpec newNode = AddNode(
                "should throw " + typeof( TException ).Name,
                () =>
                {
                    if ( parent.exception == null )
                    {
                        throw new AssertFailedException( "No exception was thrown." );
                    }
                    Assert.IsInstanceOfType( parent.exception, typeof( TException ) );
                },
                NodeType.It );

            parent = newNode.parent;
            parent.swallowExceptions = true;

            return newNode;
        }

        IEnumerable<XSpec[]> GatherTests( List<XSpec> stack )
        {
            stack.Add( this );

            if ( this.type >= NodeType.It )
            {
                yield return stack.ToArray();
                stack.RemoveAt( stack.Count - 1 );
            }

            foreach ( XSpec child in this.children )
            {
                foreach ( XSpec[] test in child.GatherTests( stack ) ) { yield return test; }
            }
        }

        /// <summary>
        /// Executes the specification.
        /// </summary>
        /// <exception cref="AssertionFailedException">The specification validation has failed.</exception>
        /// <remarks>A log of the execution is written to stdout.</remarks>
        /// <example>
        /// <code>
        ///[TestMethod]
        ///public void WhenIncrementingAnInteger()
        ///{
        ///    int x = 0;
        ///    XSpec.Given( "an integer, set to zero", () => x = 0 )
        ///        .When( "the integer is incremented", () => x++ )
        ///            .It( "can be incremented again to two", () => { x++; Assert.AreEqual( x, 2 ); } )
        ///            .It( "should be 1", () => x == 1 )
        ///        .When( "the integer is incremented again", () => x++ )
        ///            .It( "should be 2", () => x == 2 )
        ///        .When( "the integer is divided by zero", () => x = x / ( x - 2 ) )
        ///            .ItShouldThrow&lt;DivideByZeroException>()
        ///    .Go();
        ///}        
        /// </code>
        /// </example>
        public void Go()
        {
            XSpec root = this;
            while ( root.parent != null ) { root = root.parent; }
            Run( root );
        }

        bool Report( StringBuilder builder, int depth )
        {
            for ( int i = 0; i < depth; i++ ) { builder.Append( "  " ); }
            
            if ( this.type == NodeType.TheException )
            {
                builder.Append( "The exception " );
            }
            else
            {
                builder.Append( this.type.ToString() );
                builder.Append( " " );
            }

            builder.Append( this.description );
            builder.Append( " (" );

            string message = null;
            bool multiMessage = false;
            long averageMs = 0;
            if ( this.results.Count > 0 )
            {
                long totalMs = 0;
                for ( int i = 0; i < this.results.Count; i++ )
                {
                    totalMs += this.results[i].Item1;
                    if ( this.results[i].Item2 != null )
                    {
                        if ( message == null )
                        {
                            message = this.results[i].Item2;
                        }
                        else if ( !String.Equals( message, this.results[i].Item2 ) )
                        {
                            multiMessage = true;
                        }
                    }
                }
                averageMs = totalMs / this.results.Count;
            }
            builder.AppendFormat( "{0}ms, ", averageMs );

            if ( multiMessage ) { builder.Append( "+" ); }
            switch ( this.state )
            {
            case ExecState.Exception: builder.Append( "exception" ); break;
            case ExecState.Failed: builder.Append( "FAILED" ); break;
            case ExecState.NotRun: builder.Append( "not run" ); break;
            case ExecState.Passed: builder.Append( "ok" ); break;
            }
            if ( message != null )
            {
                builder.AppendFormat( ": '{0}'", message );
            }
            builder.AppendLine( ")" );

            bool result = true;
            for ( int i = 0; i < this.children.Count; i++ )
            {
                result = this.children[i].Report( builder, depth + 1 ) && result;
            }

            return result && ( this.state == ExecState.Passed );
        }

        static void Run( XSpec root )
        {
            var stack = new List<XSpec>();
            List<XSpec[]> tests = root.GatherTests( stack ).ToList();
            for ( int i = 0; i < tests.Count; i++ )
            {
                XSpec[] test = tests[i];
                for ( int j = 0; j < test.Length; j++ )
                {
                    if ( !test[j].Exec() ) { break; }
                }
            }

            var builder = new StringBuilder();
            bool passed = root.Report( builder, 0 );
            Console.WriteLine( builder.ToString() );
            if ( !passed )
            {
                throw new AssertFailedException( "\r\n" + builder.ToString() );
            }
        }

        /// <summary>
        /// Makes an assertion about an exception raised by the previous action.
        /// </summary>
        /// <param name="description">A string describing the assertion being made. This should be a short sentence
        /// that starts with 'it ...', but eliding the 'it'. For example, "puts the lotion in the bucket" would be an
        /// acceptable value.</param>
        /// <param name="assertion">The assertion to be made. It should throw an <see cref="AssertionFailedException"/> 
        /// if it fails.</param>
        /// <returns>A spec under which additional actions and assertions can be made.</returns>
        /// <remarks>This assertion has the side effect of making the preceeding action no longer fail when an 
        /// exception is thrown.</remarks> 
        public XSpec TheException( string description, Action<Exception> assertion )
        {
            if ( description == null ) { throw new ArgumentNullException( "description" ); }
            if ( assertion == null ) { throw new ArgumentNullException( "assertion" ); }

            XSpec parent = null;
            XSpec newNode = AddNode(
                description,
                () =>
                {
                    if ( parent.exception == null ) { throw new AssertFailedException( "No exception was thrown." ); }
                    assertion( parent.exception );
                },
                NodeType.TheException, NodeType.It );

            parent = newNode.parent;
            parent.swallowExceptions = true;

            return newNode;
        }

        /// <summary>
        /// Makes an assertion about an exception raised by the previous action.
        /// </summary>
        /// <param name="description">A string describing the assertion being made. This should be a short sentence
        /// that starts with 'it ...', but eliding the 'it'. For example, "puts the lotion in the bucket" would be an
        /// acceptable value.</param>
        /// <param name="assertion">The assertion to be made. It should throw an <see cref="AssertionFailedException"/> 
        /// if it fails.</param>
        /// <returns>A spec under which additional actions and assertions can be made.</returns>
        /// <remarks>This assertion has the side effect of making the preceeding action no longer fail when an 
        /// exception is thrown.</remarks> 
        public XSpec TheException( string description, Func<Exception, bool> assertion )
        {
            if ( description == null ) { throw new ArgumentNullException( "description" ); }
            if ( assertion == null ) { throw new ArgumentNullException( "assertion" ); }

            XSpec parent = null;
            XSpec newNode = AddNode(
                description,
                () =>
                {
                    if ( parent.exception == null ) { throw new AssertFailedException( "No exception was thrown." ); }
                    Assert.IsTrue( assertion( parent.exception ) );
                },
                NodeType.TheException, NodeType.It );

            parent = newNode.parent;
            parent.swallowExceptions = true;

            return newNode;
        }

        /// <summary>
        /// The executed state of a spec node. The ordering is used for precedence; higher values take precedence over 
        /// lower ones.
        /// </summary>
        enum ExecState
        {
            NotRun = 0,
            Passed = 1,
            Failed = 2,
            Exception = 3,
        }

        /// <summary>
        /// The type of a node; they're ordered so that we can get nesting and ordering right.
        /// </summary>
        enum NodeType
        {
            Given = 0,
            When = 1,
            It = 2,
            TheException = 3,
        }
    }

    [TestClass]
    public class DescribeXSpecWhen
    {
        [TestMethod]
        public void ItThrowsExceptionsOnInvalidArguments()
        {
            XSpec spec = null;

            XSpec
                .Given( "a do-nothing 'given' statement", () => { spec = XSpec.Given( "x", () => { } ); } )
                .When( "calling 'when' with a null description", () => spec.When( null, () => { } ) )
                    .ItShouldThrow<ArgumentNullException>()
                    .TheException( "should have 'description' in it", e => e.Message.Contains( "description" ) )

                .When( "calling 'when' with a null action", () => spec.When( "x", null ) )
                    .ItShouldThrow<ArgumentNullException>()
                    .TheException( "should have 'action' in it", e => e.Message.Contains( "action" ) )

                .When( "calling 'when' with an empty description", () => spec.When( "", () => { } ) )
                    .ItShouldThrow<ArgumentNullException>()
                    .TheException( "should have 'description' in it", e => e.Message.Contains( "description" ) )
            .Go();
        }

        [TestMethod]
        public void ExceptionsPreventChildrenFromRunning()
        {
            XSpec spec = null;
            bool ranAssertion = false;

            XSpec
                .Given( "a spec with a bad 'when' node with an 'it' node beneath it", () =>
                {
                    spec = XSpec
                        .Given( "x", () => { } )
                        .When( "y", () => { throw new Exception( "bad" ); } )
                            .It( "should not run this", () => { ranAssertion = true; } );
                } )
                .When( "the spec is executed", () => spec.Go() )
                    .ItShouldThrow<AssertFailedException>()
                    .It( "should not have run the 'it' node", () => !ranAssertion )
            .Go();
        }
    }

    [TestClass]
    public class DescribeXSpecIt
    {
        [TestMethod]
        public void ItThrowsExceptionsOnInvalidArguments()
        {
            XSpec spec = null;

            XSpec
                .Given( "a do-nothing 'given' statement", () => { spec = XSpec.Given( "x", () => { } ); } )

                .When( "calling 'it' (action) with a null description", () => spec.It( null, () => { } ) )
                    .ItShouldThrow<ArgumentNullException>()
                    .TheException( "should have 'description' in it", e => e.Message.Contains( "description" ) )

                .When( "calling 'it' (predicate) with a null description", () => spec.It( null, () => true ) )
                    .ItShouldThrow<ArgumentNullException>()
                    .TheException( "should have 'description' in it", e => e.Message.Contains( "description" ) )

                .When( "calling 'it' (action) with a null action", () => spec.It( "x", (Action)null ) )
                    .ItShouldThrow<ArgumentNullException>()
                    .TheException( "should have 'action' in it", e => e.Message.Contains( "action" ) )

                .When( "calling 'it' (predicate) with a null predicate", () => spec.It( "x", (Expression<Func<bool>>)null ) )
                    .ItShouldThrow<ArgumentNullException>()
                    .TheException( "should have 'predicate' in it", e => e.Message.Contains( "predicate" ) )
            .Go();
        }

        [TestMethod]
        public void SideEffectsAreUndone()
        {
            int count = 0;

            XSpec
                .Given( "a count of 23", () => count = 23 )
                .It( "should be 23", () => count == 23 )
                .It( "should let me set it to 24", () => count = 24 )
                .It( "should be 23 here, though", () => count == 23 )
            .Go();
        }
    }

    [TestClass]
    public class SampleSpec
    {
        [TestMethod]
        public void WhenIncrementingAnInteger()
        {
            int x = 0;
            XSpec.Given( "an integer, set to zero", () => x = 0 )
                .When( "the integer is incremented", () => x++ )
                    .It( "can be incremented again to two", () => { x++; Assert.AreEqual( x, 2 ); } )
                    .It( "should be 1", () => x == 1 )
                .When( "the integer is incremented again", () => x++ )
                    .It( "should be 2", () => x == 2 )
                .When( "the integer is divided by zero", () => x = x / ( x - 2 ) )
                    .ItShouldThrow<DivideByZeroException>()
            .Go();
        }
    }
}
