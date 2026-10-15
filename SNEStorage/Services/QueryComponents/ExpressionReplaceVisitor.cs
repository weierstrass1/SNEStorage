using System.Linq.Expressions;

namespace SNEStorage.Services.QueryComponents;

internal sealed class ExpressionReplaceVisitor(
    Expression source, Expression replacement) : ExpressionVisitor
{
    public override Expression? Visit(Expression? node)
    {
        return node == source ? replacement : base.Visit(node);
    }
}
