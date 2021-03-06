using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using NRules.Fluent.Dsl;
using NRules.RuleModel.Builders;

namespace NRules.Fluent.Expressions
{
    internal class QueryExpression : IQuery, IQueryBuilder
    {
        private readonly ParameterExpression _symbol;
        private readonly GroupBuilder _groupBuilder;
        private Func<IPatternContainerBuilder, string, PatternBuilder> _buildAction;

        public QueryExpression(ParameterExpression symbol, GroupBuilder groupBuilder)
        {
            _symbol = symbol;
            _groupBuilder = groupBuilder;
            Builder = this;
        }

        public IQueryBuilder Builder { get; }

        public void FactQuery<TSource>(Expression<Func<TSource, bool>>[] conditions)
        {
            _buildAction = (b, n) =>
            {
                var patternBuilder = b.Pattern(typeof(TSource), n);
                patternBuilder.DslConditions(_groupBuilder.Declarations, conditions);
                return patternBuilder;
            };
        }

        public void Where<TSource>(Expression<Func<TSource, bool>>[] predicates)
        {
            var previousBuildAction = _buildAction;
            _buildAction = (b, n) =>
            {
                var patternBuilder = previousBuildAction(b, n);
                patternBuilder.DslConditions(_groupBuilder.Declarations, predicates);
                return patternBuilder;
            };
        }

        public void Select<TSource, TResult>(Expression<Func<TSource, TResult>> selector)
        {
            var previousBuildAction = _buildAction;
            _buildAction = (b, n) =>
            {
                var aggregatePatternBuilder = b.Pattern(typeof(TResult), n);
                var aggregateBuilder = aggregatePatternBuilder.Aggregate();
                var sourceBuilder = previousBuildAction(aggregateBuilder, null);
                var selectorExpression = sourceBuilder.DslPatternExpression(_groupBuilder.Declarations, selector);
                aggregateBuilder.Project(selectorExpression);
                return aggregatePatternBuilder;
            };
        }

        public void SelectMany<TSource, TResult>(Expression<Func<TSource, IEnumerable<TResult>>> selector)
        {
            var previousBuildAction = _buildAction;
            _buildAction = (b, n) =>
            {
                var aggregatePatternBuilder = b.Pattern(typeof(TResult), n);
                var aggregateBuilder = aggregatePatternBuilder.Aggregate();
                var sourceBuilder = previousBuildAction(aggregateBuilder, null);
                var selectorExpression = sourceBuilder.DslPatternExpression(_groupBuilder.Declarations, selector);
                aggregateBuilder.Flatten(selectorExpression);
                return aggregatePatternBuilder;
            };
        }

        public void GroupBy<TSource, TKey, TElement>(Expression<Func<TSource, TKey>> keySelector, Expression<Func<TSource, TElement>> elementSelector)
        {
            var previousBuildAction = _buildAction;
            _buildAction = (b, n) =>
            {
                var aggregatePatternBuilder = b.Pattern(typeof(IGrouping<TKey, TElement>), n);
                var aggregateBuilder = aggregatePatternBuilder.Aggregate();
                var sourceBuilder = previousBuildAction(aggregateBuilder, null);
                var keySelectorExpression = sourceBuilder.DslPatternExpression(_groupBuilder.Declarations, keySelector);
                var elementSelectorExpression = sourceBuilder.DslPatternExpression(_groupBuilder.Declarations, elementSelector);
                aggregateBuilder.GroupBy(keySelectorExpression, elementSelectorExpression);
                return aggregatePatternBuilder;
            };
        }

        public void Aggregate<TSource, TResult>(string name, IDictionary<string, LambdaExpression> expressionMap, Type customFactoryType)
        {
            var previousBuildAction = _buildAction;
            _buildAction = (b, n) =>
            {
                var aggregatePatternBuilder = b.Pattern(typeof(TResult), n);
                var aggregateBuilder = aggregatePatternBuilder.Aggregate();
                var sourceBuilder = previousBuildAction(aggregateBuilder, null);

                var rewrittenExpressionMap = new Dictionary<string, LambdaExpression>();
                foreach (var expression in expressionMap)
                {
                    var lambda = sourceBuilder.DslPatternExpression(_groupBuilder.Declarations, expression.Value);
                    rewrittenExpressionMap[expression.Key] = lambda;
                }

                aggregateBuilder.Aggregator(name, rewrittenExpressionMap, customFactoryType);
                return aggregatePatternBuilder;
            };
        }
        
        public void Collect<TSource>()
        {
            var previousBuildAction = _buildAction;
            _buildAction = (b, n) =>
            {
                var aggregatePatternBuilder = b.Pattern(typeof(IEnumerable<TSource>), n);
                var aggregateBuilder = aggregatePatternBuilder.Aggregate();
                previousBuildAction(aggregateBuilder, null);
                aggregateBuilder.Collect();
                return aggregatePatternBuilder;
            };
        }

        public void Build()
        {
            _buildAction(_groupBuilder, _symbol.Name);
        }
    }

    public class QueryExpression<TSource> : IQuery<TSource>
    {
        public QueryExpression(IQueryBuilder builder)
        {
            Builder = builder;
        }

        public IQueryBuilder Builder { get; }
    }
}