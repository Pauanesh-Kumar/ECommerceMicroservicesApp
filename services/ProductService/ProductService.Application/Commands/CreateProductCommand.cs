using FluentValidation;
using MediatR;
using ProductService.Domain.Entities;
using ProductService.Domain.Repositories;

namespace ProductService.Application.Commands.CreateProduct;

public sealed record CreateProductCommand(
    string Sku, string Name, string Description, string Category,
    decimal Price, string Currency, int InitialStock) : IRequest<Guid>;

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.InitialStock).GreaterThanOrEqualTo(0);
    }
}

public sealed class CreateProductCommandHandler(IProductRepository repo) : IRequestHandler<CreateProductCommand, Guid>
{
    public async Task<Guid> Handle(CreateProductCommand cmd, CancellationToken ct)
    {
        var product = Product.Create(cmd.Sku, cmd.Name, cmd.Description, cmd.Category, cmd.Price, cmd.Currency, cmd.InitialStock);
        await repo.AddAsync(product, ct);
        return product.Id;
    }
}
