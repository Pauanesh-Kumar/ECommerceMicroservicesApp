using ProductService.Application.Commands.CreateProduct;
using ProductService.Domain.Entities;

namespace ProductService.Tests;

public class ProductTests
{
    [Fact]
    public void CreateInitializesInventory()
    {
        var product = Product.Create("SKU", "Product", "Description", "Category", 12m, "INR", 5);
        Assert.NotEqual(Guid.Empty, product.Id);
        Assert.Equal(5, product.Stock);
        Assert.Equal(0, product.ReservedStock);
        Assert.True(product.IsActive);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, -1)]
    public void ValidatorRejectsInvalidPriceOrStock(int price, int stock)
    {
        var command = new CreateProductCommand("SKU", "Product", "Description", "Category", price, "INR", stock);
        Assert.False(new CreateProductCommandValidator().Validate(command).IsValid);
    }
}
