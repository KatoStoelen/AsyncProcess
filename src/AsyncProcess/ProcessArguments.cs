using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AsyncProcess.Internal;

namespace AsyncProcess
{
    /// <summary>
    /// A collection of process arguments.
    /// </summary>
    public class ProcessArguments : IEnumerable<string>
    {
        private readonly List<ProcessArgument> _arguments = new List<ProcessArgument>();

        /// <summary>
        /// Adds a verb argument.
        /// </summary>
        /// <param name="verb">The name of the verb argument.</param>
        /// <returns>
        /// The current <see cref="ProcessArguments"/> instance for chaining purposes.
        /// </returns>
        public ProcessArguments AddVerb(string verb)
        {
            _arguments.Add(ProcessArgument.Verb(verb));

            return this;
        }

        /// <summary>
        /// Adds a noun argument.
        /// </summary>
        /// <param name="value">The value of the noun argument.</param>
        /// <returns>
        /// The current <see cref="ProcessArguments"/> instance for chaining purposes.
        /// </returns>
        public ProcessArguments AddNoun(string value)
        {
            _arguments.Add(ProcessArgument.Noun(value));

            return this;
        }

        /// <summary>
        /// Adds an option argument without value.
        /// </summary>
        /// <param name="optionName">The name of the option argument.</param>
        /// <returns>
        /// The current <see cref="ProcessArguments"/> instance for chaining purposes.
        /// </returns>
        public ProcessArguments AddOption(string optionName)
        {
            _arguments.Add(ProcessArgument.Option(optionName));

            return this;
        }

        /// <summary>
        /// Adds an option argument with value.
        /// </summary>
        /// <param name="optionName">The name of the option argument.</param>
        /// <param name="value">The value of the option argument.</param>
        /// <returns>
        /// The current <see cref="ProcessArguments"/> instance for chaining purposes.
        /// </returns>
        public ProcessArguments AddOption(string optionName, string value)
        {
            _arguments.Add(ProcessArgument.Option(optionName, value));

            return this;
        }

        /// <summary>
        /// Converts the <see cref="ProcessArguments"/> to a string.
        /// </summary>
        /// <returns>
        /// A string representation of the <see cref="ProcessArguments"/>.
        /// </returns>
        public override string ToString() => string.Join(" ", _arguments);

        /// <inheritdoc/>
        public IEnumerator<string> GetEnumerator() => _arguments
            .SelectMany(arg => arg)
            .GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Implicit string cast operator.
        /// </summary>
        /// <param name="args">A <see cref="ProcessArguments"/> instance.</param>
        /// <returns>
        /// A string representation of the <see cref="ProcessArguments"/>.
        /// </returns>
        public static implicit operator string(ProcessArguments? args) => args?.ToString() ?? string.Empty;
    }
}