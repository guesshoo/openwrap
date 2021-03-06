﻿using System;
using System.Collections;
using System.Collections.Generic;
using OpenWrap.Commands;
using System.Linq;

namespace OpenWrap.Collections
{
    public class SequenceBuilder : ISequenceBuilder
    {
        readonly List<IEnumerable<ICommandOutput>> _results = new List<IEnumerable<ICommandOutput>>();
        readonly Func<ICommandOutput, bool> _enumerationCondition;

        public SequenceBuilder(IEnumerable<ICommandOutput> resultSets) : this(resultSets, true)
        {
        }

        /// <summary>
        /// breakOnAny = true: Usual behaviour
        /// breakOnAny = false: Only error output will trigger a stop of the enumeration
        /// </summary>
        public SequenceBuilder(IEnumerable<ICommandOutput> resultSets, bool breakOnAny)
        {
            _results.Add(resultSets);
            _enumerationCondition = breakOnAny
                                            ? co => true
                                            : new Func<ICommandOutput, bool>(co => co.Type == CommandResultType.Error);

        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<ICommandOutput>)this).GetEnumerator();
        }

        IEnumerator<ICommandOutput> IEnumerable<ICommandOutput>.GetEnumerator()
        {
            foreach (var resultSet in _results)
            {
                var hadValue = false;
                var enumerator = resultSet.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    if (enumerator.Current == null)
                        continue;
                    hadValue = _enumerationCondition(enumerator.Current);
                    yield return enumerator.Current;
                }
                if (hadValue)
                    yield break;
            }
        }

        public ISequenceBuilder Or(IEnumerable<ICommandOutput> returnValue)
        {
            _results.Add(returnValue);
            return this;
        }

        public ISequenceBuilder Or(Func<ICommandOutput> returnValue)
        {
            _results.Add(returnValue.AsEnumerable());
            return this;
        }
    }
    public static class OutputExtensions
    {
        public static IEnumerable<ICommandOutput> IfSucceeds(this IEnumerable<ICommandOutput> first, IEnumerable<ICommandOutput> second)
        {
            bool errorFound = false;
            foreach(var command in first)
            {
                yield return command;
                if (command.Type == CommandResultType.Error)
                    errorFound = true;
            }
            if (errorFound) yield break;

            foreach (var output in second)
                yield return output;
        }
    }
}