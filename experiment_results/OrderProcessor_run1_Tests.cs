using System;
using System.Collections.Generic;
using Moq;
using Xunit;
using MutationAgentWorkflow.Sample;

namespace MutationAgentWorkflow.Sample.Tests
{
    public class OrderProcessorTests
    {
        private sealed class FakeOrderRepository : IOrderRepository
        {
            public Order? OrderToReturn { get; set; }
            public List<Order> OrdersToReturn { get; set; } = new();
            public Order? SavedOrder { get; private set; }
            public int SavedCallCount { get; private set; }
            public int FindByIdCallCount { get; private set; }
            public int GetByCustomerIdCallCount { get; private set; }
            public int LastFindByIdOrderId { get; private set; }
            public int LastGetByCustomerIdCustomerId { get; private set; }

            public Order? FindById(int orderId)
            {
                FindByIdCallCount++;
                LastFindByIdOrderId = orderId;
                return OrderToReturn;
            }

            public void Save(Order order)
            {
                SavedCallCount++;
                SavedOrder = order;
            }

            public List<Order> GetByCustomerId(int customerId)
            {
                GetByCustomerIdCallCount++;
                LastGetByCustomerIdCustomerId = customerId;
                return OrdersToReturn;
            }
        }

        private sealed class FakePricingService : IPricingService
        {
            public decimal DiscountToReturn { get; set; }
            public decimal TaxRateToReturn { get; set; }
            public decimal LastDiscountSubtotal { get; private set; }
            public string? LastDiscountCouponCode { get; private set; }
            public decimal LastTaxRateRegionCallCountInput { get; private set; }
            public string? LastTaxRateRegion { get; private set; }
            public int CalculateDiscountCallCount { get; private set; }
            public int GetTaxRateCallCount { get; private set; }

            public decimal CalculateDiscount(decimal subtotal, string? couponCode)
            {
                CalculateDiscountCallCount++;
                LastDiscountSubtotal = subtotal;
                LastDiscountCouponCode = couponCode;
                return DiscountToReturn;
            }

            public decimal GetTaxRate(string region)
            {
                GetTaxRateCallCount++;
                LastTaxRateRegion = region;
                LastTaxRateRegionCallCountInput = 1;
                return TaxRateToReturn;
            }
        }

        private sealed class FakeNotificationService : INotificationService
        {
            public int SendCallCount { get; private set; }
            public string? LastEmail { get; private set; }
            public int LastOrderId { get; private set; }
            public decimal LastTotal { get; private set; }

            public void SendOrderConfirmation(string email, int orderId, decimal total)
            {
                SendCallCount++;
                LastEmail = email;
                LastOrderId = orderId;
                LastTotal = total;
            }
        }

        private static OrderProcessor CreateSut(
            FakeOrderRepository? repository = null,
            FakePricingService? pricing = null,
            FakeNotificationService? notification = null)
        {
            return new OrderProcessor(
                repository ?? new FakeOrderRepository(),
                pricing ?? new FakePricingService(),
                notification ?? new FakeNotificationService());
        }

        private static Order CreateValidOrder()
        {
            return new Order
            {
                Id = 10,
                CustomerId = 1,
                CustomerEmail = "customer@example.com",
                Region = "US",
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductName = "ProductA", Quantity = 1, UnitPrice = 25m }
                }
            };
        }

        [Fact]
        public void ValidateOrder_NullOrder_ReturnsSingleError()
        {
            // Arrange
            var sut = CreateSut();

            // Act
            var errors = sut.ValidateOrder(null!);

            // Assert
            Assert.Single(errors);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void ValidateOrder_InvalidCustomerId_AddsCustomerIdError(int customerId)
        {
            // Arrange
            var sut = CreateSut();
            var order = CreateValidOrder();
            order.CustomerId = customerId;

            // Act
            var errors = sut.ValidateOrder(order);

            // Assert
            Assert.Contains("Customer ID must be positive.", errors);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ValidateOrder_MissingCustomerEmail_AddsEmailRequiredError(string? email)
        {
            // Arrange
            var sut = CreateSut();
            var order = CreateValidOrder();
            order.CustomerEmail = email!;

            // Act
            var errors = sut.ValidateOrder(order);

            // Assert
            Assert.Contains("Customer email is required.", errors);
        }

        [Fact]
        public void ValidateOrder_InvalidCustomerEmailFormat_AddsEmailValidError()
        {
            // Arrange
            var sut = CreateSut();
            var order = CreateValidOrder();
            order.CustomerEmail = "invalid-email";

            // Act
            var errors = sut.ValidateOrder(order);

            // Assert
            Assert.Contains("Customer email must be valid.", errors);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ValidateOrder_MissingRegion_AddsRegionRequiredError(string? region)
        {
            // Arrange
            var sut = CreateSut();
            var order = CreateValidOrder();
            order.Region = region!;

            // Act
            var errors = sut.ValidateOrder(order);

            // Assert
            Assert.Contains("Region is required.", errors);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(101)]
        public void ValidateOrder_InvalidItemCount_AddsCorrectCountError(int itemCount)
        {
            // Arrange
            var sut = CreateSut();
            var order = CreateValidOrder();
            order.Items = new List<OrderItem>();
            for (int i = 0; i < itemCount; i++)
            {
                order.Items.Add(new OrderItem { ProductName = "P", Quantity = 1, UnitPrice = 1m });
            }

            // Act
            var errors = sut.ValidateOrder(order);

            // Assert
            if (itemCount == 0)
                Assert.Contains("Order must contain at least one item.", errors);
            else
                Assert.Contains("Order cannot contain more than 100 items.", errors);
        }

        [Fact]
        public void ValidateOrder_ItemWithBlankProductName_AddsProductNameError()
        {
            // Arrange
            var sut = CreateSut();
            var order = CreateValidOrder();
            order.Items = new List<OrderItem>
            {
                new OrderItem { ProductName = " ", Quantity = 1, UnitPrice = 1m }
            };

            // Act
            var errors = sut.ValidateOrder(order);

            // Assert
            Assert.Contains("Item 1: product name is required.", errors);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void ValidateOrder_NonPositiveQuantity_AddsQuantityError(int quantity)
        {
            // Arrange
            var sut = CreateSut();
            var order = CreateValidOrder();
            order.Items = new List<OrderItem>
            {
                new OrderItem { ProductName = "P", Quantity = quantity, UnitPrice = 1m }
            };

            // Act
            var errors = sut.ValidateOrder(order);

            // Assert
            Assert.Contains("Item 1: quantity must be positive.", errors);
        }

        [Theory]
        [InlineData(-0.01)]
        [InlineData(-10)]
        public void ValidateOrder_NegativeUnitPrice_AddsUnitPriceError(decimal unitPrice)
        {
            // Arrange
            var sut = CreateSut();
            var order = CreateValidOrder();
            order.Items = new List<OrderItem>
            {
                new OrderItem { ProductName = "P", Quantity = 1, UnitPrice = unitPrice }
            };

            // Act
            var errors = sut.ValidateOrder(order);

            // Assert
            Assert.Contains("Item 1: unit price cannot be negative.", errors);
        }

        [Fact]
        public void ValidateOrder_ValidOrder_ReturnsNoErrors()
        {
            // Arrange
            var sut = CreateSut();
            var order = CreateValidOrder();

            // Act
            var errors = sut.ValidateOrder(order);

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void CalculateSubtotal_NullItems_ReturnsZero()
        {
            // Arrange
            var sut = CreateSut();

            // Act
            var result = sut.CalculateSubtotal(null!);

            // Assert
            Assert.Equal(0m, result);
        }

        [Fact]
        public void CalculateSubtotal_EmptyItems_ReturnsZero()
        {
            // Arrange
            var sut = CreateSut();

            // Act
            var result = sut.CalculateSubtotal(new List<OrderItem>());

            // Assert
            Assert.Equal(0m, result);
        }

        [Fact]
        public void CalculateSubtotal_InvalidItemsAreSkipped_ReturnsOnlyValidLineTotals()
        {
            // Arrange
            var sut = CreateSut();
            var items = new List<OrderItem>
            {
                new OrderItem { ProductName = "InvalidQuantity", Quantity = 0, UnitPrice = 10m },
                new OrderItem { ProductName = "InvalidPrice", Quantity = 1, UnitPrice = -1m },
                new OrderItem { ProductName = "Valid", Quantity = 2, UnitPrice = 10m }
            };

            // Act
            var result = sut.CalculateSubtotal(items);

            // Assert
            Assert.Equal(20m, result);
        }

        [Fact]
        public void CalculateSubtotal_QuantityExactly10_AppliesFivePercentDiscount()
        {
            // Arrange
            var sut = CreateSut();
            var items = new List<OrderItem>
            {
                new OrderItem { ProductName = "Bulk", Quantity = 10, UnitPrice = 10m }
            };

            // Act
            var result = sut.CalculateSubtotal(items);

            // Assert
            Assert.Equal(95m, result);
        }

        [Fact]
        public void CalculateSubtotal_RoundsToTwoDecimals_ReturnsRoundedSubtotal()
        {
            // Arrange
            var sut = CreateSut();
            var items = new List<OrderItem>
            {
                new OrderItem { ProductName = "Rounding", Quantity = 1, UnitPrice = 10.005m }
            };

            // Act
            var result = sut.CalculateSubtotal(items);

            // Assert
            Assert.Equal(10m, result);
        }

        [Theory]
        [InlineData(-0.01, "Invalid")]
        [InlineData(0, "Invalid")]
        [InlineData(49.99, "Small")]
        [InlineData(50, "Medium")]
        [InlineData(499.99, "Medium")]
        [InlineData(500, "Large")]
        [InlineData(4999.99, "Large")]
        [InlineData(5000, "Enterprise")]
        public void ClassifyOrder_BoundaryValues_ReturnsExpectedClassification(decimal total, string expected)
        {
            // Arrange
            var sut = CreateSut();

            // Act
            var result = sut.ClassifyOrder(total);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ProcessOrder_ValidationFails_ReturnsFalseAndDoesNotCallDependencies()
        {
            // Arrange
            var repository = new FakeOrderRepository();
            var pricing = new FakePricingService();
            var notification = new FakeNotificationService();
            var sut = CreateSut(repository, pricing, notification);
            var order = CreateValidOrder();
            order.CustomerEmail = "invalid";

            // Act
            var result = sut.ProcessOrder(order);

            // Assert
            Assert.False(result);
            Assert.Equal(0, repository.SavedCallCount);
            Assert.Equal(0, pricing.CalculateDiscountCallCount);
            Assert.Equal(0, notification.SendCallCount);
        }

        [Fact]
        public void ProcessOrder_SubtotalZero_ReturnsFalse()
        {
            // Arrange
            var sut = CreateSut();
            var order = CreateValidOrder();
            order.Items = new List<OrderItem>
            {
                new OrderItem { ProductName = "Invalid", Quantity = 0, UnitPrice = 10m }
            };

            // Act
            var result = sut.ProcessOrder(order);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ProcessOrder_DiscountGreaterThanSubtotal_ClampsDiscountedSubtotalToZeroAndProcesses()
        {
            // Arrange
            var repository = new FakeOrderRepository();
            var pricing = new FakePricingService { DiscountToReturn = 50m, TaxRateToReturn = 0.10m };
            var notification = new FakeNotificationService();
            var sut = CreateSut(repository, pricing, notification);
            var order = CreateValidOrder();
            order.Items = new List<OrderItem>
            {
                new OrderItem { ProductName = "Item", Quantity = 1, UnitPrice = 20m }
            };

            // Act
            var result = sut.ProcessOrder(order);

            // Assert
            Assert.True(result);
            Assert.Equal(0m, order.Tax);
            Assert.Equal(0m, order.Total);
        }

        [Fact]
        public void ProcessOrder_TaxRoundsToTwoDecimals_SetsRoundedTaxAndTotal()
        {
            // Arrange
            var repository = new FakeOrderRepository();
            var pricing = new FakePricingService { DiscountToReturn = 0m, TaxRateToReturn = 0.075m };
            var notification = new FakeNotificationService();
            var sut = CreateSut(repository, pricing, notification);
            var order = CreateValidOrder();
            order.Items = new List<OrderItem>
            {
                new OrderItem { ProductName = "Item", Quantity = 1, UnitPrice = 10m }
            };

            // Act
            var result = sut.ProcessOrder(order);

            // Assert
            Assert.True(result);
            Assert.Equal(0.75m, order.Tax);
            Assert.Equal(10.75m, order.Total);
        }

        [Fact]
        public void ProcessOrder_TotalExceedsMaximum_ReturnsFalse()
        {
            // Arrange
            var repository = new FakeOrderRepository();
            var pricing = new FakePricingService { DiscountToReturn = 0m, TaxRateToReturn = 0.25m };
            var notification = new FakeNotificationService();
            var sut = CreateSut(repository, pricing, notification);
            var order = CreateValidOrder();
            order.Items = new List<OrderItem>
            {
                new OrderItem { ProductName = "Expensive", Quantity = 1, UnitPrice = 50000m }
            };

            // Act
            var result = sut.ProcessOrder(order);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ProcessOrder_SuccessfulPath_ConfirmsOrderSavesAndSendsNotification()
        {
            // Arrange
            var repository = new FakeOrderRepository();
            var pricing = new FakePricingService { DiscountToReturn = 5m, TaxRateToReturn = 0.10m };
            var notification = new FakeNotificationService();
            var sut = CreateSut(repository, pricing, notification);
            var order = CreateValidOrder();
            order.Id = 42;
            order.Items = new List<OrderItem>
            {
                new OrderItem { ProductName = "Item", Quantity = 1, UnitPrice = 50m }
            };

            // Act
            var result = sut.ProcessOrder(order);

            // Assert
            Assert.True(result);
            Assert.Equal(OrderStatus.Confirmed, order.Status);
            Assert.Equal(order, repository.SavedOrder);
            Assert.Equal(1, notification.SendCallCount);
        }

        [Fact]
        public void CancelOrder_OrderNotFound_ReturnsFalse()
        {
            // Arrange
            var repository = new FakeOrderRepository { OrderToReturn = null };
            var sut = CreateSut(repository);

            // Act
            var result = sut.CancelOrder(1);

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData(OrderStatus.Pending)]
        [InlineData(OrderStatus.Cancelled)]
        [InlineData(OrderStatus.Refunded)]
        public void CancelOrder_OrderNotConfirmed_ReturnsFalse(OrderStatus status)
        {
            // Arrange
            var repository = new FakeOrderRepository
            {
                OrderToReturn = new Order { Id = 1, Status = status }
            };
            var sut = CreateSut(repository);

            // Act
            var result = sut.CancelOrder(1);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void CancelOrder_ConfirmedOrder_UpdatesStatusAndSaves()
        {
            // Arrange
            var order = new Order { Id = 1, Status = OrderStatus.Confirmed };
            var repository = new FakeOrderRepository { OrderToReturn = order };
            var sut = CreateSut(repository);

            // Act
            var result = sut.CancelOrder(1);

            // Assert
            Assert.True(result);
            Assert.Equal(OrderStatus.Cancelled, order.Status);
            Assert.Equal(1, repository.SavedCallCount);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void GetCustomerTotalSpent_InvalidCustomerId_ReturnsZero(int customerId)
        {
            // Arrange
            var sut = CreateSut();

            // Act
            var result = sut.GetCustomerTotalSpent(customerId);

            // Assert
            Assert.Equal(0m, result);
        }

        [Fact]
        public void GetCustomerTotalSpent_RepositoryReturnsNull_ReturnsZero()
        {
            // Arrange
            var repository = new FakeOrderRepository { OrdersToReturn = null! };
            var sut = CreateSut(repository);

            // Act
            var result = sut.GetCustomerTotalSpent(1);

            // Assert
            Assert.Equal(0m, result);
        }

        [Fact]
        public void GetCustomerTotalSpent_EmptyOrderList_ReturnsZero()
        {
            // Arrange
            var repository = new FakeOrderRepository { OrdersToReturn = new List<Order>() };
            var sut = CreateSut(repository);

            // Act
            var result = sut.GetCustomerTotalSpent(1);

            // Assert
            Assert.Equal(0m, result);
        }

        [Fact]
        public void GetCustomerTotalSpent_OnlyConfirmedOrdersAreCounted_ReturnsConfirmedTotal()
        {
            // Arrange
            var repository = new FakeOrderRepository
            {
                OrdersToReturn = new List<Order>
                {
                    new Order { Status = OrderStatus.Confirmed, Total = 100m },
                    new Order { Status = OrderStatus.Cancelled, Total = 200m },
                    new Order { Status = OrderStatus.Pending, Total = 300m },
                    new Order { Status = OrderStatus.Refunded, Total = 400m }
                }
            };
            var sut = CreateSut(repository);

            // Act
            var result = sut.GetCustomerTotalSpent(1);

            // Assert
            Assert.Equal(100m, result);
        }

        [Fact]
        public void GetCustomerTotalSpent_MultipleConfirmedOrders_ReturnsSummedTotal()
        {
            // Arrange
            var repository = new FakeOrderRepository
            {
                OrdersToReturn = new List<Order>
                {
                    new Order { Status = OrderStatus.Confirmed, Total = 10.25m },
                    new Order { Status = OrderStatus.Confirmed, Total = 20.75m }
                }
            };
            var sut = CreateSut(repository);

            // Act
            var result = sut.GetCustomerTotalSpent(1);

            // Assert
            Assert.Equal(31m, result);
        }
    }
}

namespace MutationAgentWorkflow.Tests.Integration
{
    public class OrderProcessorIntegrationTests
    {
        [Fact]
        public void CalculateSubtotal_NullItems_ReturnsZeroAndDoesNotUseDependencies()
        {
            // Arrange
            var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
            var pricingServiceMock = new Mock<IPricingService>(MockBehavior.Strict);
            var notificationServiceMock = new Mock<INotificationService>(MockBehavior.Strict);

            var sut = new OrderProcessor(
                repositoryMock.Object,
                pricingServiceMock.Object,
                notificationServiceMock.Object);

            // Act
            var result = sut.CalculateSubtotal(null!);

            // Assert
            Assert.Equal(0m, result);
            repositoryMock.VerifyNoOtherCalls();
            pricingServiceMock.VerifyNoOtherCalls();
            notificationServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void CalculateSubtotal_MixedValidAndInvalidItems_SkipsInvalidItemsAndRoundsToTwoDecimals()
        {
            // Arrange
            var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
            var pricingServiceMock = new Mock<IPricingService>(MockBehavior.Strict);
            var notificationServiceMock = new Mock<INotificationService>(MockBehavior.Strict);

            var sut = new OrderProcessor(
                repositoryMock.Object,
                pricingServiceMock.Object,
                notificationServiceMock.Object);

            var items = new List<OrderItem>
            {
                new OrderItem { ProductName = "ValidSmall", Quantity = 2, UnitPrice = 10.00m },
                new OrderItem { ProductName = "InvalidQty", Quantity = 0, UnitPrice = 100m },
                new OrderItem { ProductName = "InvalidPrice", Quantity = 3, UnitPrice = -1m },
                new OrderItem { ProductName = "BulkDiscount", Quantity = 10, UnitPrice = 3.33m }
            };

            // Act
            var result = sut.CalculateSubtotal(items);

            // Assert
            Assert.Equal(51.64m, result);
            repositoryMock.VerifyNoOtherCalls();
            pricingServiceMock.VerifyNoOtherCalls();
            notificationServiceMock.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData(0, "Invalid")]
        [InlineData(-0.01, "Invalid")]
        [InlineData(49.99, "Small")]
        [InlineData(50, "Medium")]
        [InlineData(499.99, "Medium")]
        [InlineData(500, "Large")]
        [InlineData(4999.99, "Large")]
        [InlineData(5000, "Enterprise")]
        public void ClassifyOrder_BoundaryValues_ReturnsExpectedClassification(decimal total, string expected)
        {
            // Arrange
            var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
            var pricingServiceMock = new Mock<IPricingService>(MockBehavior.Strict);
            var notificationServiceMock = new Mock<INotificationService>(MockBehavior.Strict);

            var sut = new OrderProcessor(
                repositoryMock.Object,
                pricingServiceMock.Object,
                notificationServiceMock.Object);

            // Act
            var result = sut.ClassifyOrder(total);

            // Assert
            Assert.Equal(expected, result);
            repositoryMock.VerifyNoOtherCalls();
            pricingServiceMock.VerifyNoOtherCalls();
            notificationServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void ValidateOrder_NullOrder_ReturnsSingleErrorAndDoesNotCallDependencies()
        {
            // Arrange
            var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
            var pricingServiceMock = new Mock<IPricingService>(MockBehavior.Strict);
            var notificationServiceMock = new Mock<INotificationService>(MockBehavior.Strict);

            var sut = new OrderProcessor(
                repositoryMock.Object,
                pricingServiceMock.Object,
                notificationServiceMock.Object);

            // Act
            var errors = sut.ValidateOrder(null!);

            // Assert
            Assert.Single(errors);
            Assert.Equal("Order cannot be null.", errors[0]);
            repositoryMock.VerifyNoOtherCalls();
            pricingServiceMock.VerifyNoOtherCalls();
            notificationServiceMock.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData(0, "Customer ID must be positive.")]
        [InlineData(-1, "Customer ID must be positive.")]
        public void ValidateOrder_InvalidCustomerId_ReturnsCustomerIdError(int customerId, string expectedError)
        {
            // Arrange
            var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
            var pricingServiceMock = new Mock<IPricingService>(MockBehavior.Strict);
            var notificationServiceMock = new Mock<INotificationService>(MockBehavior.Strict);

            var sut = new OrderProcessor(
                repositoryMock.Object,
                pricingServiceMock.Object,
                notificationServiceMock.Object);

            var order = new Order
            {
                CustomerId = customerId,
                CustomerEmail = "customer@example.com",
                Region = "EU",
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductName = "Widget", Quantity = 1, UnitPrice = 10m }
                }
            };

            // Act
            var errors = sut.ValidateOrder(order);

            // Assert
            Assert.Contains(expectedError, errors);
            repositoryMock.VerifyNoOtherCalls();
            pricingServiceMock.VerifyNoOtherCalls();
            notificationServiceMock.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void ValidateOrder_MissingEmail_ReturnsEmailRequiredError(string email)
        {
            // Arrange
            var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
            var pricingServiceMock = new Mock<IPricingService>(MockBehavior.Strict);
            var notificationServiceMock = new Mock<INotificationService>(MockBehavior.Strict);

            var sut = new OrderProcessor(
                repositoryMock.Object,
                pricingServiceMock.Object,
                notificationServiceMock.Object);

            var order = new Order
            {
                CustomerId = 1,
                CustomerEmail = email,
                Region = "EU",
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductName = "Widget", Quantity = 1, UnitPrice = 10m }
                }
            };

            // Act
            var errors = sut.ValidateOrder(order);

            // Assert
            Assert.Contains("Customer email is required.", errors);
            repositoryMock.VerifyNoOtherCalls();
            pricingServiceMock.VerifyNoOtherCalls();
            notificationServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void ValidateOrder_InvalidEmailFormat_ReturnsEmailInvalidError()
        {
            // Arrange
            var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
            var pricingServiceMock = new Mock<IPricingService>(MockBehavior.Strict);
            var notificationServiceMock = new Mock<INotificationService>(MockBehavior.Strict);

            var sut = new OrderProcessor(
                repositoryMock.Object,
                pricingServiceMock.Object,
                notificationServiceMock.Object);

            var order = new Order
            {
                CustomerId = 1,
                CustomerEmail = "invalid-email",
                Region = "EU",
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductName = "Widget", Quantity = 1, UnitPrice = 10m }
                }
            };

            // Act
            var errors = sut.ValidateOrder(order);

            // Assert
            Assert.Contains("Customer email must be valid.", errors);
            repositoryMock.VerifyNoOtherCalls();
            pricingServiceMock.VerifyNoOtherCalls();
            notificationServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void ValidateOrder_MissingRegion_ReturnsRegionRequiredError()
        {
            // Arrange
            var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
            var pricingServiceMock = new Mock<IPricingService>(MockBehavior.Strict);
            var notificationServiceMock = new Mock<INotificationService>(MockBehavior.Strict);

            var sut = new OrderProcessor(
                repositoryMock.Object,
                pricingServiceMock.Object,
                notificationServiceMock.Object);

            var order = new Order
            {
                CustomerId = 1,
                CustomerEmail = "customer@example.com",
                Region = "",
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductName = "Widget", Quantity = 1, UnitPrice = 10m }
                }
            };

            // Act
            var errors = sut.ValidateOrder(order);

            // Assert
            Assert.Contains("Region is required.", errors);
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

            var sut = new OrderProcessor(
                repositoryMock.Object,
                pricingServiceMock.Object,
                notificationServiceMock.Object);

            var items = new List<OrderItem>();
            for (int i = 0; i < 101; i++)
            {
                items.Add(new OrderItem { ProductName = $"Item{i}", Quantity = 1, UnitPrice = 1m });
            }

            var order = new Order
            {
                CustomerId = 1,
                CustomerEmail = "customer@example.com",
                Region = "EU",
                Items = items
            };

            // Act
            var errors = sut.ValidateOrder(order);

            // Assert
            Assert.Contains("Order cannot contain more than 100 items.", errors);
            repositoryMock.VerifyNoOtherCalls();
            pricingServiceMock.VerifyNoOtherCalls();
            notificationServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void ValidateOrder_InvalidItemFields_ReturnsItemSpecificErrors()
        {
            // Arrange
            var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
            var pricingServiceMock = new Mock<IPricingService>(MockBehavior.Strict);
            var notificationServiceMock = new Mock<INotificationService>(MockBehavior.Strict);

            var sut = new OrderProcessor(
                repositoryMock.Object,
                pricingServiceMock.Object,
                notificationServiceMock.Object);

            var order = new Order
            {
                CustomerId = 1,
                CustomerEmail = "customer@example.com",
                Region = "EU",
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductName = " ", Quantity = 0, UnitPrice = -1m }
                }
            };

            // Act
            var errors = sut.ValidateOrder(order);

            // Assert
            Assert.Contains("Item 1: product name is required.", errors);
            Assert.Contains("Item 1: quantity must be positive.", errors);
            Assert.Contains("Item 1: unit price cannot be negative.", errors);
            repositoryMock.VerifyNoOtherCalls();
            pricingServiceMock.VerifyNoOtherCalls();
            notificationServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void ProcessOrder_ValidationFails_ReturnsFalseAndSkipsAllDependencies()
        {
            // Arrange
            var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
            var pricingServiceMock = new Mock<IPricingService>(MockBehavior.Strict);
            var notificationServiceMock = new Mock<INotificationService>(MockBehavior.Strict);

            var sut = new OrderProcessor(
                repositoryMock.Object,
                pricingServiceMock.Object,
                notificationServiceMock.Object);

            var order = new Order
            {
                CustomerId = 0,
                CustomerEmail = "bad",
                Region = "",
                Items = new List<OrderItem>()
            };

            // Act
            var result = sut.ProcessOrder(order);

            // Assert
            Assert.False(result);
            Assert.Equal(0m, order.Subtotal);
            Assert.Equal(0m, order.Discount);
            Assert.Equal(0m, order.Tax);
            Assert.Equal(0m, order.Total);
            Assert.Equal(OrderStatus.Pending, order.Status);
            repositoryMock.Verify(r => r.Save(It.IsAny<Order>()), Times.Never);
            repositoryMock.VerifyNoOtherCalls();
            pricingServiceMock.Verify(p => p.CalculateDiscount(It.IsAny<decimal>(), It.IsAny<string?>()), Times.Never);
            pricingServiceMock.Verify(p => p.GetTaxRate(It.IsAny<string>()), Times.Never);
            pricingServiceMock.VerifyNoOtherCalls();
            notificationServiceMock.Verify(n => n.SendOrderConfirmation(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<decimal>()), Times.Never);
            notificationServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void ProcessOrder_SubtotalZero_ReturnsFalseAndDoesNotCallPricingRepositoryOrNotification()
        {
            // Arrange
            var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
            var pricingServiceMock = new Mock<IPricingService>(MockBehavior.Strict);
            var notificationServiceMock = new Mock<INotificationService>(MockBehavior.Strict);

            var sut = new OrderProcessor(
                repositoryMock.Object,
                pricingServiceMock.Object,
                notificationServiceMock.Object);

            var order = new Order
            {
                Id = 10,
                CustomerId = 1,
                CustomerEmail = "customer@example.com",
                Region = "EU",
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductName = "Invalid", Quantity = 0, UnitPrice = 10m }
                }
            };

            // Act
            var result = sut.ProcessOrder(order);

            // Assert
            Assert.False(result);
            Assert.Equal(0m, order.Subtotal);
            repositoryMock.Verify(r => r.Save(It.IsAny<Order>()), Times.Never);
            repositoryMock.VerifyNoOtherCalls();
            pricingServiceMock.Verify(p => p.CalculateDiscount(It.IsAny<decimal>(), It.IsAny<string?>()), Times.Never);
            pricingServiceMock.Verify(p => p.GetTaxRate(It.IsAny<string>()), Times.Never);
            pricingServiceMock.VerifyNoOtherCalls();
            notificationServiceMock.Verify(n => n.SendOrderConfirmation(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<decimal>()), Times.Never);
            notificationServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void ProcessOrder_SuccessfulPath_TransformsOrderPersistsAndSendsNotification()
        {
            // Arrange
            var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
            var pricingServiceMock = new Mock<IPricingService>(MockBehavior.Strict);
            var notificationServiceMock = new Mock<INotificationService>(MockBehavior.Strict);

            pricingServiceMock
                .Setup(p => p.CalculateDiscount(95m, "SAVE10"))
                .Returns(10m);

            pricingServiceMock
                .Setup(p => p.GetTaxRate("EU"))
                .Returns(0.2m);

            repositoryMock
                .Setup(r => r.Save(It.IsAny<Order>()));

            notificationServiceMock
                .Setup(n => n.SendOrderConfirmation("customer@example.com", 123, 102m));

            var sut = new OrderProcessor(
                repositoryMock.Object,
                pricingServiceMock.Object,
                notificationServiceMock.Object);

            var order = new Order
            {
                Id = 123,
                CustomerId = 1,
                CustomerEmail = "customer@example.com",
                Region = "EU",
                CouponCode = "SAVE10",
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductName = "Bulk", Quantity = 10, UnitPrice = 10m }
                }
            };

            // Act
            var result = sut.ProcessOrder(order);

            // Assert
            Assert.True(result);
            Assert.Equal(95m, order.Subtotal);
            Assert.Equal(10m, order.Discount);
            Assert.Equal(17m, order.Tax);
            Assert.Equal(102m, order.Total);
            Assert.Equal(OrderStatus.Confirmed, order.Status);
            Assert.NotEqual(default, order.CreatedAt);
            Assert.True((DateTime.UtcNow - order.CreatedAt).TotalSeconds < 5);

            pricingServiceMock.Verify(p => p.CalculateDiscount(
                It.Is<decimal>(s => s == 95m),
                It.Is<string?>(c => c == "SAVE10")), Times.Once);

            pricingServiceMock.Verify(p => p.GetTaxRate(
                It.Is<string>(r => r == "EU")), Times.Once);

            repositoryMock.Verify(r => r.Save(It.Is<Order>(o =>
                o.Id == 123 &&
                o.CustomerId == 1 &&
                o.CustomerEmail == "customer@example.com" &&
                o.Region == "EU" &&
                o.CouponCode == "SAVE10" &&
                o.Subtotal == 95m &&
                o.Discount == 10m &&
                o.Tax == 17m &&
                o.Total == 102m &&
                o.Status == OrderStatus.Confirmed &&
                o.CreatedAt != default &&
                o.Items.Count == 1 &&
                o.Items[0].ProductName == "Bulk" &&
                o.Items[0].Quantity == 10 &&
                o.Items[0].UnitPrice == 10m)), Times.Once);

            notificationServiceMock.Verify(n => n.SendOrderConfirmation(
                It.Is<string>(e => e == "customer@example.com"),
                It.Is<int>(id => id == 123),
                It.Is<decimal>(t => t == 102m)), Times.Once);

            repositoryMock.VerifyNoOtherCalls();
            pricingServiceMock.VerifyNoOtherCalls();
            notificationServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void ProcessOrder_DiscountExceedsSubtotal_ClampsDiscountedSubtotalAndCompletesWithTax()
        {
            // Arrange
            var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
            var pricingServiceMock = new Mock<IPricingService>(MockBehavior.Strict);
            var notificationServiceMock = new Mock<INotificationService>(MockBehavior.Strict);

            pricingServiceMock
                .Setup(p => p.CalculateDiscount(10m, null))
                .Returns(25m);

            pricingServiceMock
                .Setup(p => p.GetTaxRate("EU"))
                .Returns(0.2m);

            repositoryMock
                .Setup(r => r.Save(It.IsAny<Order>()));

            notificationServiceMock
                .Setup(n => n.SendOrderConfirmation("customer@example.com", 7, 0m));

            var sut = new OrderProcessor(
                repositoryMock.Object,
                pricingServiceMock.Object,
                notificationServiceMock.Object);

            var order = new Order
            {
                Id = 7,
                CustomerId = 2,
                CustomerEmail = "customer@example.com",
                Region = "EU",
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductName = "Item", Quantity = 1, UnitPrice = 10m }
                }
            };

            // Act
            var result = sut.ProcessOrder(order);

            // Assert
            Assert.True(result);
            Assert.Equal(10m, order.Subtotal);
            Assert.Equal(25m, order.Discount);
            Assert.Equal(0m, order.Tax);
            Assert.Equal(0m, order.Total);
            Assert.Equal(OrderStatus.Confirmed, order.Status);

            pricingServiceMock.Verify(p => p.CalculateDiscount(10m, null), Times.Once);
            pricingServiceMock.Verify(p => p.GetTaxRate("EU"), Times.Once);
            repositoryMock.Verify(r => r.Save(It.Is<Order>(o => o.Total == 0m && o.Discount == 25m && o.Tax == 0m)), Times.Once);
            notificationServiceMock.Verify(n => n.SendOrderConfirmation("customer@example.com", 7, 0m), Times.Once);

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

            pricingServiceMock
                .Setup(p => p.CalculateDiscount(50000m, null))
                .Returns(0m);

            pricingServiceMock
                .Setup(p => p.GetTaxRate("EU"))
                .Returns(0.01m);

            var sut = new OrderProcessor(
                repositoryMock.Object,
                pricingServiceMock.Object,
                notificationServiceMock.Object);

            var order = new Order
            {
                Id = 99,
                CustomerId = 1,
                CustomerEmail = "customer@example.com",
                Region = "EU",
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductName = "Expensive", Quantity = 1, UnitPrice = 50000m }
                }
            };

            // Act
            var result = sut.ProcessOrder(order);

            // Assert
            Assert.False(result);
            Assert.Equal(50000m, order.Subtotal);
            Assert.Equal(0m, order.Discount);
            Assert.Equal(500m, order.Tax);
            Assert.Equal(50500m, order.Total);
            Assert.Equal(OrderStatus.Pending, order.Status);

            pricingServiceMock.Verify(p => p.CalculateDiscount(50000m, null), Times.Once);
            pricingServiceMock.Verify(p => p.GetTaxRate("EU"), Times.Once);
            repositoryMock.Verify(r => r.Save(It.IsAny<Order>()), Times.Never);
            notificationServiceMock.Verify(n => n.SendOrderConfirmation(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<decimal>()), Times.Never);

            repositoryMock.VerifyNoOtherCalls();
            pricingServiceMock.VerifyNoOtherCalls();
            notificationServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void CancelOrder_OrderNotFound_ReturnsFalseAndDoesNotSave()
        {
            // Arrange
            var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
            var pricingServiceMock = new Mock<IPricingService>(MockBehavior.Strict);
            var notificationServiceMock = new Mock<INotificationService>(MockBehavior.Strict);

            repositoryMock
                .Setup(r => r.FindById(42))
                .Returns((Order?)null);

            var sut = new OrderProcessor(
                repositoryMock.Object,
                pricingServiceMock.Object,
                notificationServiceMock.Object);

            // Act
            var result = sut.CancelOrder(42);

            // Assert
            Assert.False(result);
            repositoryMock.Verify(r => r.FindById(It.Is<int>(id => id == 42)), Times.Once);
            repositoryMock.Verify(r => r.Save(It.IsAny<Order>()), Times.Never);
            pricingServiceMock.VerifyNoOtherCalls();
            notificationServiceMock.VerifyNoOtherCalls();
            repositoryMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void CancelOrder_OrderNotConfirmed_ReturnsFalseAndDoesNotSave()
        {
            // Arrange
            var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
            var pricingServiceMock = new Mock<IPricingService>(MockBehavior.Strict);
            var notificationServiceMock = new Mock<INotificationService>(MockBehavior.Strict);

            repositoryMock
                .Setup(r => r.FindById(42))
                .Returns(new Order { Id = 42, Status = OrderStatus.Pending });

            var sut = new OrderProcessor(
                repositoryMock.Object,
                pricingServiceMock.Object,
                notificationServiceMock.Object);

            // Act
            var result = sut.CancelOrder(42);

            // Assert
            Assert.False(result);
            repositoryMock.Verify(r => r.FindById(42), Times.Once);
            repositoryMock.Verify(r => r.Save(It.IsAny<Order>()), Times.Never);
            pricingServiceMock.VerifyNoOtherCalls();
            notificationServiceMock.VerifyNoOtherCalls();
            repositoryMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void CancelOrder_ConfirmedOrder_ChangesStatusAndPersists()
        {
            // Arrange
            var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
            var pricingServiceMock = new Mock<IPricingService>(MockBehavior.Strict);
            var notificationServiceMock = new Mock<INotificationService>(MockBehavior.Strict);

            var order = new Order { Id = 42, Status = OrderStatus.Confirmed };

            repositoryMock
                .Setup(r => r.FindById(42))
                .Returns(order);

            repositoryMock
                .Setup(r => r.Save(It.IsAny<Order>()));

            var sut = new OrderProcessor(
                repositoryMock.Object,
                pricingServiceMock.Object,
                notificationServiceMock.Object);

            // Act
            var result = sut.CancelOrder(42);

            // Assert
            Assert.True(result);
            Assert.Equal(OrderStatus.Cancelled, order.Status);

            repositoryMock.Verify(r => r.FindById(It.Is<int>(id => id == 42)), Times.Once);
            repositoryMock.Verify(r => r.Save(It.Is<Order>(o =>
                o.Id == 42 &&
                o.Status == OrderStatus.Cancelled)), Times.Once);
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

            var sut = new OrderProcessor(
                repositoryMock.Object,
                pricingServiceMock.Object,
                notificationServiceMock.Object);

            // Act
            var result = sut.GetCustomerTotalSpent(customerId);

            // Assert
            Assert.Equal(0m, result);
            repositoryMock.Verify(r => r.GetByCustomerId(It.IsAny<int>()), Times.Never);
            repositoryMock.VerifyNoOtherCalls();
            pricingServiceMock.VerifyNoOtherCalls();
            notificationServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void GetCustomerTotalSpent_NoOrders_ReturnsZero()
        {
            // Arrange
            var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
            var pricingServiceMock = new Mock<IPricingService>(MockBehavior.Strict);
            var notificationServiceMock = new Mock<INotificationService>(MockBehavior.Strict);

            repositoryMock
                .Setup(r => r.GetByCustomerId(10))
                .Returns(new List<Order>());

            var sut = new OrderProcessor(
                repositoryMock.Object,
                pricingServiceMock.Object,
                notificationServiceMock.Object);

            // Act
            var result = sut.GetCustomerTotalSpent(10);

            // Assert
            Assert.Equal(0m, result);
            repositoryMock.Verify(r => r.GetByCustomerId(It.Is<int>(id => id == 10)), Times.Once);
            pricingServiceMock.VerifyNoOtherCalls();
            notificationServiceMock.VerifyNoOtherCalls();
            repositoryMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void GetCustomerTotalSpent_OnlyConfirmedOrdersCountsConfirmedTotals()
        {
            // Arrange
            var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
            var pricingServiceMock = new Mock<IPricingService>(MockBehavior.Strict);
            var notificationServiceMock = new Mock<INotificationService>(MockBehavior.Strict);

            repositoryMock
                .Setup(r => r.GetByCustomerId(10))
                .Returns(new List<Order>
                {
                    new Order { Id = 1, Status = OrderStatus.Confirmed, Total = 100m },
                    new Order { Id = 2, Status = OrderStatus.Cancelled, Total = 50m },
                    new Order { Id = 3, Status = OrderStatus.Refunded, Total = 25m },
                    new Order { Id = 4, Status = OrderStatus.Pending, Total = 10m },
                    new Order { Id = 5, Status = OrderStatus.Confirmed, Total = 75.5m }
                });

            var sut = new OrderProcessor(
                repositoryMock.Object,
                pricingServiceMock.Object,
                notificationServiceMock.Object);

            // Act
            var result = sut.GetCustomerTotalSpent(10);

            // Assert
            Assert.Equal(175.5m, result);
            repositoryMock.Verify(r => r.GetByCustomerId(10), Times.Once);
            pricingServiceMock.VerifyNoOtherCalls();
            notificationServiceMock.VerifyNoOtherCalls();
            repositoryMock.VerifyNoOtherCalls();
        }
    }
}