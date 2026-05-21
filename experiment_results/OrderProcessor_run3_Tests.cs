using System;
using System.Collections.Generic;
using Moq;
using Xunit;
using MutationAgentWorkflow.Sample;

namespace MutationAgentWorkflow.Tests;

public class OrderProcessorTests
{
    private sealed class FakeOrderRepository : IOrderRepository
    {
        public Order? OrderToReturn { get; set; }
        public List<Order>? OrdersByCustomerToReturn { get; set; } = new();
        public Order? SavedOrder { get; private set; }
        public int? FindByIdCalledWith { get; private set; }
        public int? GetByCustomerIdCalledWith { get; private set; }
        public int SaveCallCount { get; private set; }

        public Order? FindById(int orderId)
        {
            FindByIdCalledWith = orderId;
            return OrderToReturn;
        }

        public void Save(Order order)
        {
            SaveCallCount++;
            SavedOrder = order;
        }

        public List<Order> GetByCustomerId(int customerId)
        {
            GetByCustomerIdCalledWith = customerId;
            return OrdersByCustomerToReturn ?? new List<Order>();
        }
    }

    private sealed class FakePricingService : IPricingService
    {
        public decimal DiscountToReturn { get; set; }
        public decimal TaxRateToReturn { get; set; }
        public decimal? CalculateDiscountCalledWithSubtotal { get; private set; }
        public string? CalculateDiscountCalledWithCouponCode { get; private set; }
        public string? GetTaxRateCalledWithRegion { get; private set; }

        public decimal CalculateDiscount(decimal subtotal, string? couponCode)
        {
            CalculateDiscountCalledWithSubtotal = subtotal;
            CalculateDiscountCalledWithCouponCode = couponCode;
            return DiscountToReturn;
        }

        public decimal GetTaxRate(string region)
        {
            GetTaxRateCalledWithRegion = region;
            return TaxRateToReturn;
        }
    }

    private sealed class FakeNotificationService : INotificationService
    {
        public int CallCount { get; private set; }
        public string? Email { get; private set; }
        public int OrderId { get; private set; }
        public decimal Total { get; private set; }

        public void SendOrderConfirmation(string email, int orderId, decimal total)
        {
            CallCount++;
            Email = email;
            OrderId = orderId;
            Total = total;
        }
    }

    private static OrderProcessor CreateSut(
        FakeOrderRepository repository,
        FakePricingService pricingService,
        FakeNotificationService notificationService)
    {
        return new OrderProcessor(repository, pricingService, notificationService);
    }

    [Fact]
    public void CalculateSubtotal_NullItems_ReturnsZero()
    {
        // Arrange
        var sut = CreateSut(new FakeOrderRepository(), new FakePricingService(), new FakeNotificationService());

        // Act
        var result = sut.CalculateSubtotal(null!);

        // Assert
        Assert.Equal(0m, result);
    }

    [Fact]
    public void CalculateSubtotal_EmptyItems_ReturnsZero()
    {
        // Arrange
        var sut = CreateSut(new FakeOrderRepository(), new FakePricingService(), new FakeNotificationService());

        // Act
        var result = sut.CalculateSubtotal(new List<OrderItem>());

        // Assert
        Assert.Equal(0m, result);
    }

    [Theory]
    [InlineData(1, 10m, 10m)]
    [InlineData(10, 10m, 95m)]
    public void CalculateSubtotal_ValidItems_UsesBulkDiscountBoundary(int quantity, decimal unitPrice, decimal expected)
    {
        // Arrange
        var sut = CreateSut(new FakeOrderRepository(), new FakePricingService(), new FakeNotificationService());
        var items = new List<OrderItem>
        {
            new OrderItem { ProductName = "P1", Quantity = quantity, UnitPrice = unitPrice }
        };

        // Act
        var result = sut.CalculateSubtotal(items);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CalculateSubtotal_InvalidItems_SkipsNegativeAndZeroValues()
    {
        // Arrange
        var sut = CreateSut(new FakeOrderRepository(), new FakePricingService(), new FakeNotificationService());
        var items = new List<OrderItem>
        {
            new OrderItem { ProductName = "A", Quantity = 0, UnitPrice = 10m },
            new OrderItem { ProductName = "B", Quantity = 1, UnitPrice = -1m },
            new OrderItem { ProductName = "C", Quantity = 2, UnitPrice = 5m }
        };

        // Act
        var result = sut.CalculateSubtotal(items);

        // Assert
        Assert.Equal(10m, result);
    }

    [Fact]
    public void CalculateSubtotal_RoundsToTwoDecimals_ReturnsRoundedSubtotal()
    {
        // Arrange
        var sut = CreateSut(new FakeOrderRepository(), new FakePricingService(), new FakeNotificationService());
        var items = new List<OrderItem>
        {
            new OrderItem { ProductName = "A", Quantity = 1, UnitPrice = 10.005m }
        };

        // Act
        var result = sut.CalculateSubtotal(items);

        // Assert
        Assert.Equal(10.01m, result);
    }

    [Theory]
    [InlineData(0d, "Invalid")]
    [InlineData(-0.01d, "Invalid")]
    [InlineData(0.01d, "Small")]
    [InlineData(49.99d, "Small")]
    [InlineData(50d, "Medium")]
    [InlineData(499.99d, "Medium")]
    [InlineData(500d, "Large")]
    [InlineData(4999.99d, "Large")]
    [InlineData(5000d, "Enterprise")]
    public void ClassifyOrder_BoundaryValues_ReturnsExpectedClassification(decimal total, string expected)
    {
        // Arrange
        var sut = CreateSut(new FakeOrderRepository(), new FakePricingService(), new FakeNotificationService());

        // Act
        var result = sut.ClassifyOrder(total);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ValidateOrder_NullOrder_ReturnsSingleNullError()
    {
        // Arrange
        var sut = CreateSut(new FakeOrderRepository(), new FakePricingService(), new FakeNotificationService());

        // Act
        var result = sut.ValidateOrder(null!);

        // Assert
        Assert.Single(result);
        Assert.Contains("Order cannot be null.", result);
    }

    [Fact]
    public void ValidateOrder_ValidOrder_ReturnsNoErrors()
    {
        // Arrange
        var sut = CreateSut(new FakeOrderRepository(), new FakePricingService(), new FakeNotificationService());
        var order = new Order
        {
            CustomerId = 1,
            CustomerEmail = "test@example.com",
            Region = "US",
            Items = new List<OrderItem>
            {
                new OrderItem { ProductName = "Item", Quantity = 1, UnitPrice = 10m }
            }
        };

        // Act
        var result = sut.ValidateOrder(order);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ValidateOrder_InvalidCustomerId_AddsCustomerIdError()
    {
        // Arrange
        var sut = CreateSut(new FakeOrderRepository(), new FakePricingService(), new FakeNotificationService());
        var order = new Order
        {
            CustomerId = 0,
            CustomerEmail = "test@example.com",
            Region = "US",
            Items = new List<OrderItem>
            {
                new OrderItem { ProductName = "Item", Quantity = 1, UnitPrice = 10m }
            }
        };

        // Act
        var result = sut.ValidateOrder(order);

        // Assert
        Assert.Contains("Customer ID must be positive.", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateOrder_EmptyOrWhitespaceCustomerEmail_AddsRequiredError(string email)
    {
        // Arrange
        var sut = CreateSut(new FakeOrderRepository(), new FakePricingService(), new FakeNotificationService());
        var order = new Order
        {
            CustomerId = 1,
            CustomerEmail = email,
            Region = "US",
            Items = new List<OrderItem>
            {
                new OrderItem { ProductName = "Item", Quantity = 1, UnitPrice = 10m }
            }
        };

        // Act
        var result = sut.ValidateOrder(order);

        // Assert
        Assert.Contains("Customer email is required.", result);
    }

    [Fact]
    public void ValidateOrder_InvalidCustomerEmailFormat_AddsValidEmailError()
    {
        // Arrange
        var sut = CreateSut(new FakeOrderRepository(), new FakePricingService(), new FakeNotificationService());
        var order = new Order
        {
            CustomerId = 1,
            CustomerEmail = "test.example.com",
            Region = "US",
            Items = new List<OrderItem>
            {
                new OrderItem { ProductName = "Item", Quantity = 1, UnitPrice = 10m }
            }
        };

        // Act
        var result = sut.ValidateOrder(order);

        // Assert
        Assert.Contains("Customer email must be valid.", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateOrder_EmptyOrWhitespaceRegion_AddsRequiredError(string region)
    {
        // Arrange
        var sut = CreateSut(new FakeOrderRepository(), new FakePricingService(), new FakeNotificationService());
        var order = new Order
        {
            CustomerId = 1,
            CustomerEmail = "test@example.com",
            Region = region,
            Items = new List<OrderItem>
            {
                new OrderItem { ProductName = "Item", Quantity = 1, UnitPrice = 10m }
            }
        };

        // Act
        var result = sut.ValidateOrder(order);

        // Assert
        Assert.Contains("Region is required.", result);
    }

    [Fact]
    public void ValidateOrder_NullItems_AddsItemsRequiredError()
    {
        // Arrange
        var sut = CreateSut(new FakeOrderRepository(), new FakePricingService(), new FakeNotificationService());
        var order = new Order
        {
            CustomerId = 1,
            CustomerEmail = "test@example.com",
            Region = "US",
            Items = null!
        };

        // Act
        var result = sut.ValidateOrder(order);

        // Assert
        Assert.Contains("Order must contain at least one item.", result);
    }

    [Fact]
    public void ValidateOrder_EmptyItems_AddsItemsRequiredError()
    {
        // Arrange
        var sut = CreateSut(new FakeOrderRepository(), new FakePricingService(), new FakeNotificationService());
        var order = new Order
        {
            CustomerId = 1,
            CustomerEmail = "test@example.com",
            Region = "US",
            Items = new List<OrderItem>()
        };

        // Act
        var result = sut.ValidateOrder(order);

        // Assert
        Assert.Contains("Order must contain at least one item.", result);
    }

    [Fact]
    public void ValidateOrder_MoreThanMaxItems_AddsMaxItemsError()
    {
        // Arrange
        var sut = CreateSut(new FakeOrderRepository(), new FakePricingService(), new FakeNotificationService());
        var items = new List<OrderItem>();
        for (int i = 0; i < 101; i++)
        {
            items.Add(new OrderItem { ProductName = "Item", Quantity = 1, UnitPrice = 1m });
        }
        var order = new Order
        {
            CustomerId = 1,
            CustomerEmail = "test@example.com",
            Region = "US",
            Items = items
        };

        // Act
        var result = sut.ValidateOrder(order);

        // Assert
        Assert.Contains("Order cannot contain more than 100 items.", result);
    }

    [Fact]
    public void ValidateOrder_ItemWithInvalidFields_AddsItemSpecificErrors()
    {
        // Arrange
        var sut = CreateSut(new FakeOrderRepository(), new FakePricingService(), new FakeNotificationService());
        var order = new Order
        {
            CustomerId = 1,
            CustomerEmail = "test@example.com",
            Region = "US",
            Items = new List<OrderItem>
            {
                new OrderItem { ProductName = "", Quantity = 0, UnitPrice = -1m }
            }
        };

        // Act
        var result = sut.ValidateOrder(order);

        // Assert
        Assert.Contains("Item 1: product name is required.", result);
    }

    [Fact]
    public void ProcessOrder_InvalidOrder_ReturnsFalse()
    {
        // Arrange
        var repository = new FakeOrderRepository();
        var pricingService = new FakePricingService();
        var notificationService = new FakeNotificationService();
        var sut = CreateSut(repository, pricingService, notificationService);
        var order = new Order();

        // Act
        var result = sut.ProcessOrder(order);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ProcessOrder_SubtotalEqualsZero_ReturnsFalse()
    {
        // Arrange
        var repository = new FakeOrderRepository();
        var pricingService = new FakePricingService();
        var notificationService = new FakeNotificationService();
        var sut = CreateSut(repository, pricingService, notificationService);
        var order = new Order
        {
            CustomerId = 1,
            CustomerEmail = "test@example.com",
            Region = "US",
            Items = new List<OrderItem>
            {
                new OrderItem { ProductName = "Item", Quantity = 1, UnitPrice = 0m }
            }
        };

        // Act
        var result = sut.ProcessOrder(order);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ProcessOrder_DiscountedSubtotalBelowZero_ClampsAndProcessesSuccessfully()
    {
        // Arrange
        var repository = new FakeOrderRepository();
        var pricingService = new FakePricingService { DiscountToReturn = 20m, TaxRateToReturn = 0.1m };
        var notificationService = new FakeNotificationService();
        var sut = CreateSut(repository, pricingService, notificationService);
        var order = new Order
        {
            Id = 7,
            CustomerId = 1,
            CustomerEmail = "test@example.com",
            Region = "US",
            Items = new List<OrderItem>
            {
                new OrderItem { ProductName = "Item", Quantity = 1, UnitPrice = 10m }
            }
        };

        // Act
        var result = sut.ProcessOrder(order);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ProcessOrder_ValidOrder_SavesConfirmsAndSendsNotification()
    {
        // Arrange
        var repository = new FakeOrderRepository();
        var pricingService = new FakePricingService { DiscountToReturn = 1m, TaxRateToReturn = 0.1m };
        var notificationService = new FakeNotificationService();
        var sut = CreateSut(repository, pricingService, notificationService);
        var order = new Order
        {
            Id = 42,
            CustomerId = 1,
            CustomerEmail = "test@example.com",
            Region = "US",
            CouponCode = "SAVE",
            Items = new List<OrderItem>
            {
                new OrderItem { ProductName = "Item", Quantity = 1, UnitPrice = 10m }
            }
        };

        // Act
        var result = sut.ProcessOrder(order);

        // Assert
        Assert.True(result);
        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.Equal(order, repository.SavedOrder);
        Assert.Equal(1, notificationService.CallCount);
    }

    [Fact]
    public void ProcessOrder_TotalExceedsMaximum_ReturnsFalse()
    {
        // Arrange
        var repository = new FakeOrderRepository();
        var pricingService = new FakePricingService { DiscountToReturn = 0m, TaxRateToReturn = 0.1m };
        var notificationService = new FakeNotificationService();
        var sut = CreateSut(repository, pricingService, notificationService);
        var order = new Order
        {
            CustomerId = 1,
            CustomerEmail = "test@example.com",
            Region = "US",
            Items = new List<OrderItem>
            {
                new OrderItem { ProductName = "Item", Quantity = 1, UnitPrice = 50001m }
            }
        };

        // Act
        var result = sut.ProcessOrder(order);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void CancelOrder_OrderNotFound_ReturnsFalse()
    {
        // Arrange
        var repository = new FakeOrderRepository { OrderToReturn = null };
        var sut = CreateSut(repository, new FakePricingService(), new FakeNotificationService());

        // Act
        var result = sut.CancelOrder(10);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void CancelOrder_NonConfirmedOrder_ReturnsFalse()
    {
        // Arrange
        var repository = new FakeOrderRepository
        {
            OrderToReturn = new Order { Id = 10, Status = OrderStatus.Pending }
        };
        var sut = CreateSut(repository, new FakePricingService(), new FakeNotificationService());

        // Act
        var result = sut.CancelOrder(10);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void CancelOrder_ConfirmedOrder_UpdatesStatusAndSaves()
    {
        // Arrange
        var order = new Order { Id = 10, Status = OrderStatus.Confirmed };
        var repository = new FakeOrderRepository { OrderToReturn = order };
        var sut = CreateSut(repository, new FakePricingService(), new FakeNotificationService());

        // Act
        var result = sut.CancelOrder(10);

        // Assert
        Assert.True(result);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal(order, repository.SavedOrder);
    }

    [Fact]
    public void GetCustomerTotalSpent_InvalidCustomerId_ReturnsZero()
    {
        // Arrange
        var sut = CreateSut(new FakeOrderRepository(), new FakePricingService(), new FakeNotificationService());

        // Act
        var result = sut.GetCustomerTotalSpent(0);

        // Assert
        Assert.Equal(0m, result);
    }

    [Fact]
    public void GetCustomerTotalSpent_NullOrderList_ReturnsZero()
    {
        // Arrange
        var repository = new FakeOrderRepository { OrdersByCustomerToReturn = null };
        var sut = CreateSut(repository, new FakePricingService(), new FakeNotificationService());

        // Act
        var result = sut.GetCustomerTotalSpent(1);

        // Assert
        Assert.Equal(0m, result);
    }

    [Fact]
    public void GetCustomerTotalSpent_OnlyConfirmedOrdersCounts_ReturnsConfirmedTotalOnly()
    {
        // Arrange
        var repository = new FakeOrderRepository
        {
            OrdersByCustomerToReturn = new List<Order>
            {
                new Order { Status = OrderStatus.Confirmed, Total = 10m },
                new Order { Status = OrderStatus.Pending, Total = 20m },
                new Order { Status = OrderStatus.Confirmed, Total = 30m }
            }
        };
        var sut = CreateSut(repository, new FakePricingService(), new FakeNotificationService());

        // Act
        var result = sut.GetCustomerTotalSpent(1);

        // Assert
        Assert.Equal(40m, result);
    }

    [Fact]
    public void GetCustomerTotalSpent_EmptyOrderList_ReturnsZero()
    {
        // Arrange
        var repository = new FakeOrderRepository
        {
            OrdersByCustomerToReturn = new List<Order>()
        };
        var sut = CreateSut(repository, new FakePricingService(), new FakeNotificationService());

        // Act
        var result = sut.GetCustomerTotalSpent(1);

        // Assert
        Assert.Equal(0m, result);
    }
}

namespace MutationAgentWorkflow.Sample.Tests;

public class OrderProcessorIntegrationTests
{
    [Fact]
    public void CalculateSubtotal_NullItems_ReturnsZeroAndDoesNotCallDependencies()
    {
        // Arrange
        var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
        var pricingServiceMock = new Mock<IPricingService>(MockBehavior.Strict);
        var notificationServiceMock = new Mock<INotificationService>(MockBehavior.Strict);

        var sut = new OrderProcessor(repositoryMock.Object, pricingServiceMock.Object, notificationServiceMock.Object);

        // Act
        var result = sut.CalculateSubtotal(null!);

        // Assert
        Assert.Equal(0m, result);
        repositoryMock.VerifyNoOtherCalls();
        pricingServiceMock.VerifyNoOtherCalls();
        notificationServiceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void CalculateSubtotal_EmptyItems_ReturnsZeroAndDoesNotCallDependencies()
    {
        // Arrange
        var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
        var pricingServiceMock = new Mock<IPricingService>(MockBehavior.Strict);
        var notificationServiceMock = new Mock<INotificationService>(MockBehavior.Strict);

        var sut = new OrderProcessor(repositoryMock.Object, pricingServiceMock.Object, notificationServiceMock.Object);

        // Act
        var result = sut.CalculateSubtotal(new List<OrderItem>());

        // Assert
        Assert.Equal(0m, result);
        repositoryMock.VerifyNoOtherCalls();
        pricingServiceMock.VerifyNoOtherCalls();
        notificationServiceMock.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(0, 10m, 0m)]
    [InlineData(-1, 10m, 0m)]
    [InlineData(2, -5m, 0m)]
    public void CalculateSubtotal_InvalidItemData_IgnoresInvalidLines(int quantity, decimal unitPrice, decimal expected)
    {
        // Arrange
        var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
        var pricingServiceMock = new Mock<IPricingService>(MockBehavior.Strict);
        var notificationServiceMock = new Mock<INotificationService>(MockBehavior.Strict);

        var sut = new OrderProcessor(repositoryMock.Object, pricingServiceMock.Object, notificationServiceMock.Object);
        var items = new List<OrderItem>
        {
            new OrderItem { ProductName = "BadItem", Quantity = quantity, UnitPrice = unitPrice }
        };

        // Act
        var result = sut.CalculateSubtotal(items);

        // Assert
        Assert.Equal(expected, result);
        repositoryMock.VerifyNoOtherCalls();
        pricingServiceMock.VerifyNoOtherCalls();
        notificationServiceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void CalculateSubtotal_BulkDiscountAndRounding_AppliesDiscountAndRoundsToTwoDecimals()
    {
        // Arrange
        var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
        var pricingServiceMock = new Mock<IPricingService>(MockBehavior.Strict);
        var notificationServiceMock = new Mock<INotificationService>(MockBehavior.Strict);

        var sut = new OrderProcessor(repositoryMock.Object, pricingServiceMock.Object, notificationServiceMock.Object);
        var items = new List<OrderItem>
        {
            new OrderItem { ProductName = "Regular", Quantity = 1, UnitPrice = 10.005m },
            new OrderItem { ProductName = "Bulk", Quantity = 10, UnitPrice = 10.00m }
        };

        // Act
        var result = sut.CalculateSubtotal(items);

        // Assert
        Assert.Equal(104.50m, result);
        repositoryMock.VerifyNoOtherCalls();
        pricingServiceMock.VerifyNoOtherCalls();
        notificationServiceMock.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(-0.01d, "Invalid")]
    [InlineData(0d, "Invalid")]
    [InlineData(0.01d, "Small")]
    [InlineData(49.99d, "Small")]
    [InlineData(50d, "Medium")]
    [InlineData(499.99d, "Medium")]
    [InlineData(500d, "Large")]
    [InlineData(4999.99d, "Large")]
    [InlineData(5000d, "Enterprise")]
    [InlineData(50000d, "Enterprise")]
    public void ClassifyOrder_BoundaryValues_ReturnsExpectedClassification(decimal total, string expected)
    {
        // Arrange
        var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
        var pricingServiceMock = new Mock<IPricingService>(MockBehavior.Strict);
        var notificationServiceMock = new Mock<INotificationService>(MockBehavior.Strict);

        var sut = new OrderProcessor(repositoryMock.Object, pricingServiceMock.Object, notificationServiceMock.Object);

        // Act
        var result = sut.ClassifyOrder(total);

        // Assert
        Assert.Equal(expected, result);
        repositoryMock.VerifyNoOtherCalls();
        pricingServiceMock.VerifyNoOtherCalls();
        notificationServiceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void ValidateOrder_NullOrder_ReturnsSingleError()
    {
        // Arrange
        var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
        var pricingServiceMock = new Mock<IPricingService>(MockBehavior.Strict);
        var notificationServiceMock = new Mock<INotificationService>(MockBehavior.Strict);

        var sut = new OrderProcessor(repositoryMock.Object, pricingServiceMock.Object, notificationServiceMock.Object);

        // Act
        var errors = sut.ValidateOrder(null!);

        // Assert
        Assert.Single(errors);
        Assert.Contains("Order cannot be null.", errors);
        repositoryMock.VerifyNoOtherCalls();
        pricingServiceMock.VerifyNoOtherCalls();
        notificationServiceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void ValidateOrder_InvalidFieldsAndItems_ReturnsAllExpectedErrors()
    {
        // Arrange
        var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
        var pricingServiceMock = new Mock<IPricingService>(MockBehavior.Strict);
        var notificationServiceMock = new Mock<INotificationService>(MockBehavior.Strict);

        var sut = new OrderProcessor(repositoryMock.Object, pricingServiceMock.Object, notificationServiceMock.Object);
        var order = new Order
        {
            CustomerId = 0,
            CustomerEmail = "invalid-email",
            Region = " ",
            Items = new List<OrderItem>
            {
                new OrderItem { ProductName = " ", Quantity = 0, UnitPrice = -1m }
            }
        };

        // Act
        var errors = sut.ValidateOrder(order);

        // Assert
        Assert.Contains("Customer ID must be positive.", errors);
        Assert.Contains("Customer email must be valid.", errors);
        Assert.Contains("Region is required.", errors);
        Assert.Contains("Item 1: product name is required.", errors);
        Assert.Contains("Item 1: quantity must be positive.", errors);
        Assert.Contains("Item 1: unit price cannot be negative.", errors);
        Assert.Equal(6, errors.Count);
        repositoryMock.VerifyNoOtherCalls();
        pricingServiceMock.VerifyNoOtherCalls();
        notificationServiceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void ValidateOrder_TooManyItems_ReturnsMaxItemsError()
    {
        // Arrange
        var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
        var pricingServiceMock = new Mock<IPricingService>(MockBehavior.Strict);
        var notificationServiceMock = new Mock<INotificationService>(MockBehavior.Strict);

        var sut = new OrderProcessor(repositoryMock.Object, pricingServiceMock.Object, notificationServiceMock.Object);
        var order = new Order
        {
            CustomerId = 1,
            CustomerEmail = "test@example.com",
            Region = "US",
            Items = new List<OrderItem>()
        };

        for (int i = 0; i < 101; i++)
        {
            order.Items.Add(new OrderItem { ProductName = $"P{i}", Quantity = 1, UnitPrice = 1m });
        }

        // Act
        var errors = sut.ValidateOrder(order);

        // Assert
        Assert.Single(errors);
        Assert.Contains("Order cannot contain more than 100 items.", errors);
        repositoryMock.VerifyNoOtherCalls();
        pricingServiceMock.VerifyNoOtherCalls();
        notificationServiceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void ProcessOrder_ValidOrder_CalculatesAndPersistsAndNotifies()
    {
        // Arrange
        var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
        var pricingServiceMock = new Mock<IPricingService>(MockBehavior.Strict);
        var notificationServiceMock = new Mock<INotificationService>(MockBehavior.Strict);

        var order = new Order
        {
            Id = 123,
            CustomerId = 42,
            CustomerEmail = "customer@example.com",
            Region = "US",
            CouponCode = "SAVE10",
            Items = new List<OrderItem>
            {
                new OrderItem { ProductName = "Widget", Quantity = 2, UnitPrice = 100m }
            }
        };

        pricingServiceMock
            .Setup(x => x.CalculateDiscount(200m, "SAVE10"))
            .Returns(20m);

        pricingServiceMock
            .Setup(x => x.GetTaxRate("US"))
            .Returns(0.10m);

        repositoryMock
            .Setup(x => x.Save(It.Is<Order>(o =>
                o == order &&
                o.Subtotal == 200m &&
                o.Discount == 20m &&
                o.Tax == 18m &&
                o.Total == 198m &&
                o.Status == OrderStatus.Confirmed &&
                o.CreatedAt != default)))
            .Verifiable();

        notificationServiceMock
            .Setup(x => x.SendOrderConfirmation("customer@example.com", 123, 198m))
            .Verifiable();

        var sut = new OrderProcessor(repositoryMock.Object, pricingServiceMock.Object, notificationServiceMock.Object);

        // Act
        var result = sut.ProcessOrder(order);

        // Assert
        Assert.True(result);
        Assert.Equal(200m, order.Subtotal);
        Assert.Equal(20m, order.Discount);
        Assert.Equal(18m, order.Tax);
        Assert.Equal(198m, order.Total);
        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.NotEqual(default, order.CreatedAt);

        pricingServiceMock.Verify(x => x.CalculateDiscount(200m, "SAVE10"), Times.Once);
        pricingServiceMock.Verify(x => x.GetTaxRate("US"), Times.Once);
        repositoryMock.Verify(x => x.Save(It.Is<Order>(o =>
            o == order &&
            o.Subtotal == 200m &&
            o.Discount == 20m &&
            o.Tax == 18m &&
            o.Total == 198m &&
            o.Status == OrderStatus.Confirmed)), Times.Once);
        notificationServiceMock.Verify(x => x.SendOrderConfirmation("customer@example.com", 123, 198m), Times.Once);

        pricingServiceMock.VerifyNoOtherCalls();
        repositoryMock.VerifyNoOtherCalls();
        notificationServiceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void ProcessOrder_ValidationFails_DoesNotCallPricingRepositoryOrNotification()
    {
        // Arrange
        var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
        var pricingServiceMock = new Mock<IPricingService>(MockBehavior.Strict);
        var notificationServiceMock = new Mock<INotificationService>(MockBehavior.Strict);

        var order = new Order
        {
            Id = 1,
            CustomerId = 0,
            CustomerEmail = "bad",
            Region = "",
            Items = new List<OrderItem>()
        };

        var sut = new OrderProcessor(repositoryMock.Object, pricingServiceMock.Object, notificationServiceMock.Object);

        // Act
        var result = sut.ProcessOrder(order);

        // Assert
        Assert.False(result);
        Assert.Equal(0m, order.Subtotal);
        Assert.Equal(0m, order.Discount);
        Assert.Equal(0m, order.Tax);
        Assert.Equal(0m, order.Total);
        Assert.Equal(OrderStatus.Pending, order.Status);

        pricingServiceMock.Verify(x => x.CalculateDiscount(It.IsAny<decimal>(), It.IsAny<string?>()), Times.Never);
        pricingServiceMock.Verify(x => x.GetTaxRate(It.IsAny<string>()), Times.Never);
        repositoryMock.Verify(x => x.Save(It.IsAny<Order>()), Times.Never);
        notificationServiceMock.Verify(x => x.SendOrderConfirmation(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<decimal>()), Times.Never);

        repositoryMock.VerifyNoOtherCalls();
        pricingServiceMock.VerifyNoOtherCalls();
        notificationServiceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void ProcessOrder_DiscountExceedsSubtotal_ClampsDiscountedSubtotalToZero()
    {
        // Arrange
        var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
        var pricingServiceMock = new Mock<IPricingService>(MockBehavior.Strict);
        var notificationServiceMock = new Mock<INotificationService>(MockBehavior.Strict);

        var order = new Order
        {
            Id = 5,
            CustomerId = 10,
            CustomerEmail = "a@b.com",
            Region = "EU",
            Items = new List<OrderItem>
            {
                new OrderItem { ProductName = "Item", Quantity = 1, UnitPrice = 100m }
            }
        };

        pricingServiceMock.Setup(x => x.CalculateDiscount(100m, null)).Returns(150m);
        pricingServiceMock.Setup(x => x.GetTaxRate("EU")).Returns(0.2m);

        repositoryMock.Setup(x => x.Save(It.Is<Order>(o =>
            o == order &&
            o.Subtotal == 100m &&
            o.Discount == 150m &&
            o.Tax == 0m &&
            o.Total == 0m &&
            o.Status == OrderStatus.Confirmed)))
            .Verifiable();

        notificationServiceMock.Setup(x => x.SendOrderConfirmation("a@b.com", 5, 0m)).Verifiable();

        var sut = new OrderProcessor(repositoryMock.Object, pricingServiceMock.Object, notificationServiceMock.Object);

        // Act
        var result = sut.ProcessOrder(order);

        // Assert
        Assert.True(result);
        Assert.Equal(100m, order.Subtotal);
        Assert.Equal(150m, order.Discount);
        Assert.Equal(0m, order.Tax);
        Assert.Equal(0m, order.Total);
        Assert.Equal(OrderStatus.Confirmed, order.Status);

        pricingServiceMock.Verify(x => x.CalculateDiscount(100m, null), Times.Once);
        pricingServiceMock.Verify(x => x.GetTaxRate("EU"), Times.Once);
        repositoryMock.Verify(x => x.Save(It.Is<Order>(o => o == order && o.Total == 0m && o.Status == OrderStatus.Confirmed)), Times.Once);
        notificationServiceMock.Verify(x => x.SendOrderConfirmation("a@b.com", 5, 0m), Times.Once);

        repositoryMock.VerifyNoOtherCalls();
        pricingServiceMock.VerifyNoOtherCalls();
        notificationServiceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void ProcessOrder_TotalExceedsMaximum_ReturnsFalseAndDoesNotPersistOrNotify()
    {
        // Arrange
        var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
        var pricingServiceMock = new Mock<IPricingService>(MockBehavior.Strict);
        var notificationServiceMock = new Mock<INotificationService>(MockBehavior.Strict);

        var order = new Order
        {
            Id = 9,
            CustomerId = 77,
            CustomerEmail = "big@example.com",
            Region = "US",
            Items = new List<OrderItem>
            {
                new OrderItem { ProductName = "Expensive", Quantity = 1, UnitPrice = 50000m }
            }
        };

        pricingServiceMock.Setup(x => x.CalculateDiscount(50000m, null)).Returns(0m);
        pricingServiceMock.Setup(x => x.GetTaxRate("US")).Returns(0.05m);

        var sut = new OrderProcessor(repositoryMock.Object, pricingServiceMock.Object, notificationServiceMock.Object);

        // Act
        var result = sut.ProcessOrder(order);

        // Assert
        Assert.False(result);
        Assert.Equal(50000m, order.Subtotal);
        Assert.Equal(0m, order.Discount);
        Assert.Equal(2500m, order.Tax);
        Assert.Equal(52500m, order.Total);
        Assert.Equal(OrderStatus.Pending, order.Status);

        pricingServiceMock.Verify(x => x.CalculateDiscount(50000m, null), Times.Once);
        pricingServiceMock.Verify(x => x.GetTaxRate("US"), Times.Once);
        repositoryMock.Verify(x => x.Save(It.IsAny<Order>()), Times.Never);
        notificationServiceMock.Verify(x => x.SendOrderConfirmation(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<decimal>()), Times.Never);

        repositoryMock.VerifyNoOtherCalls();
        pricingServiceMock.VerifyNoOtherCalls();
        notificationServiceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void CancelOrder_ConfirmedOrder_UpdatesStatusAndSaves()
    {
        // Arrange
        var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
        var pricingServiceMock = new Mock<IPricingService>(MockBehavior.Strict);
        var notificationServiceMock = new Mock<INotificationService>(MockBehavior.Strict);

        var order = new Order
        {
            Id = 88,
            Status = OrderStatus.Confirmed
        };

        repositoryMock.Setup(x => x.FindById(88)).Returns(order);
        repositoryMock.Setup(x => x.Save(It.Is<Order>(o => o == order && o.Status == OrderStatus.Cancelled))).Verifiable();

        var sut = new OrderProcessor(repositoryMock.Object, pricingServiceMock.Object, notificationServiceMock.Object);

        // Act
        var result = sut.CancelOrder(88);

        // Assert
        Assert.True(result);
        Assert.Equal(OrderStatus.Cancelled, order.Status);

        repositoryMock.Verify(x => x.FindById(88), Times.Once);
        repositoryMock.Verify(x => x.Save(It.Is<Order>(o => o == order && o.Status == OrderStatus.Cancelled)), Times.Once);
        pricingServiceMock.VerifyNoOtherCalls();
        notificationServiceMock.VerifyNoOtherCalls();
        repositoryMock.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void GetCustomerTotalSpent_InvalidCustomerId_ReturnsZeroAndDoesNotQueryRepository(int customerId)
    {
        // Arrange
        var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
        var pricingServiceMock = new Mock<IPricingService>(MockBehavior.Strict);
        var notificationServiceMock = new Mock<INotificationService>(MockBehavior.Strict);

        var sut = new OrderProcessor(repositoryMock.Object, pricingServiceMock.Object, notificationServiceMock.Object);

        // Act
        var result = sut.GetCustomerTotalSpent(customerId);

        // Assert
        Assert.Equal(0m, result);
        repositoryMock.Verify(x => x.GetByCustomerId(It.IsAny<int>()), Times.Never);
        repositoryMock.VerifyNoOtherCalls();
        pricingServiceMock.VerifyNoOtherCalls();
        notificationServiceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void GetCustomerTotalSpent_ConfirmedAndNonConfirmedOrders_SumsOnlyConfirmedOrders()
    {
        // Arrange
        var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
        var pricingServiceMock = new Mock<IPricingService>(MockBehavior.Strict);
        var notificationServiceMock = new Mock<INotificationService>(MockBehavior.Strict);

        var orders = new List<Order>
        {
            new Order { Id = 1, Status = OrderStatus.Confirmed, Total = 10m },
            new Order { Id = 2, Status = OrderStatus.Pending, Total = 20m },
            new Order { Id = 3, Status = OrderStatus.Confirmed, Total = 30.5m },
            new Order { Id = 4, Status = OrderStatus.Cancelled, Total = 40m }
        };

        repositoryMock.Setup(x => x.GetByCustomerId(99)).Returns(orders);

        var sut = new OrderProcessor(repositoryMock.Object, pricingServiceMock.Object, notificationServiceMock.Object);

        // Act
        var result = sut.GetCustomerTotalSpent(99);

        // Assert
        Assert.Equal(40.5m, result);
        repositoryMock.Verify(x => x.GetByCustomerId(99), Times.Once);
        pricingServiceMock.VerifyNoOtherCalls();
        notificationServiceMock.VerifyNoOtherCalls();
        repositoryMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void GetCustomerTotalSpent_NullOrderList_ReturnsZero()
    {
        // Arrange
        var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
        var pricingServiceMock = new Mock<IPricingService>(MockBehavior.Strict);
        var notificationServiceMock = new Mock<INotificationService>(MockBehavior.Strict);

        repositoryMock.Setup(x => x.GetByCustomerId(101)).Returns((List<Order>)null!);

        var sut = new OrderProcessor(repositoryMock.Object, pricingServiceMock.Object, notificationServiceMock.Object);

        // Act
        var result = sut.GetCustomerTotalSpent(101);

        // Assert
        Assert.Equal(0m, result);
        repositoryMock.Verify(x => x.GetByCustomerId(101), Times.Once);
        pricingServiceMock.VerifyNoOtherCalls();
        notificationServiceMock.VerifyNoOtherCalls();
        repositoryMock.VerifyNoOtherCalls();
    }
}