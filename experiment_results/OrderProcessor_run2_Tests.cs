using System;
using System.Collections.Generic;
using MutationAgentWorkflow.Sample;
using Moq;
using Xunit;

namespace MutationAgentWorkflow.Tests
{
    public class OrderProcessorTests
    {
        private sealed class FakeOrderRepository : IOrderRepository
        {
            public Order? OrderToFind { get; set; }
            public List<Order>? OrdersByCustomer { get; set; } = new();
            public int FindByIdCallCount { get; private set; }
            public int SaveCallCount { get; private set; }
            public int GetByCustomerIdCallCount { get; private set; }
            public Order? SavedOrder { get; private set; }
            public int LastFindByIdOrderId { get; private set; }
            public int LastGetByCustomerId { get; private set; }

            public Order? FindById(int orderId)
            {
                FindByIdCallCount++;
                LastFindByIdOrderId = orderId;
                return OrderToFind;
            }

            public void Save(Order order)
            {
                SaveCallCount++;
                SavedOrder = order;
            }

            public List<Order> GetByCustomerId(int customerId)
            {
                GetByCustomerIdCallCount++;
                LastGetByCustomerId = customerId;
                return OrdersByCustomer!;
            }
        }

        private sealed class FakePricingService : IPricingService
        {
            public decimal DiscountToReturn { get; set; }
            public decimal TaxRateToReturn { get; set; }
            public int CalculateDiscountCallCount { get; private set; }
            public int GetTaxRateCallCount { get; private set; }
            public decimal LastDiscountSubtotal { get; private set; }
            public string? LastDiscountCouponCode { get; private set; }
            public string? LastTaxRegion { get; private set; }

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
                LastTaxRegion = region;
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

        private static OrderProcessor CreateProcessor(
            FakeOrderRepository? repository = null,
            FakePricingService? pricingService = null,
            FakeNotificationService? notificationService = null)
        {
            return new OrderProcessor(
                repository ?? new FakeOrderRepository(),
                pricingService ?? new FakePricingService(),
                notificationService ?? new FakeNotificationService());
        }

        [Fact]
        public void ValidateOrder_NullOrder_ReturnsSingleError()
        {
            // Arrange
            var processor = CreateProcessor();

            // Act
            var errors = processor.ValidateOrder(null!);

            // Assert
            Assert.Single(errors);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void ValidateOrder_InvalidCustomerId_AddsCustomerIdError(int customerId)
        {
            // Arrange
            var processor = CreateProcessor();
            var order = new Order
            {
                CustomerId = customerId,
                CustomerEmail = "user@example.com",
                Region = "EU",
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductName = "Product", Quantity = 1, UnitPrice = 10m }
                }
            };

            // Act
            var errors = processor.ValidateOrder(order);

            // Assert
            Assert.Contains("Customer ID must be positive.", errors);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ValidateOrder_MissingCustomerEmail_AddsCustomerEmailRequiredError(string? email)
        {
            // Arrange
            var processor = CreateProcessor();
            var order = new Order
            {
                CustomerId = 1,
                CustomerEmail = email ?? string.Empty,
                Region = "EU",
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductName = "Product", Quantity = 1, UnitPrice = 10m }
                }
            };

            // Act
            var errors = processor.ValidateOrder(order);

            // Assert
            Assert.Contains("Customer email is required.", errors);
        }

        [Fact]
        public void ValidateOrder_InvalidCustomerEmailFormat_AddsCustomerEmailValidError()
        {
            // Arrange
            var processor = CreateProcessor();
            var order = new Order
            {
                CustomerId = 1,
                CustomerEmail = "invalid-email",
                Region = "EU",
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductName = "Product", Quantity = 1, UnitPrice = 10m }
                }
            };

            // Act
            var errors = processor.ValidateOrder(order);

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
            var processor = CreateProcessor();
            var order = new Order
            {
                CustomerId = 1,
                CustomerEmail = "user@example.com",
                Region = region ?? string.Empty,
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductName = "Product", Quantity = 1, UnitPrice = 10m }
                }
            };

            // Act
            var errors = processor.ValidateOrder(order);

            // Assert
            Assert.Contains("Region is required.", errors);
        }

        [Fact]
        public void ValidateOrder_NullItems_AddsItemsRequiredError()
        {
            // Arrange
            var processor = CreateProcessor();
            var order = new Order
            {
                CustomerId = 1,
                CustomerEmail = "user@example.com",
                Region = "EU",
                Items = null!
            };

            // Act
            var errors = processor.ValidateOrder(order);

            // Assert
            Assert.Contains("Order must contain at least one item.", errors);
        }

        [Fact]
        public void ValidateOrder_EmptyItems_AddsItemsRequiredError()
        {
            // Arrange
            var processor = CreateProcessor();
            var order = new Order
            {
                CustomerId = 1,
                CustomerEmail = "user@example.com",
                Region = "EU",
                Items = new List<OrderItem>()
            };

            // Act
            var errors = processor.ValidateOrder(order);

            // Assert
            Assert.Contains("Order must contain at least one item.", errors);
        }

        [Fact]
        public void ValidateOrder_ItemsCountExactlyMaxItems_NoCountError()
        {
            // Arrange
            var processor = CreateProcessor();
            var items = new List<OrderItem>();
            for (int i = 0; i < 100; i++)
            {
                items.Add(new OrderItem { ProductName = $"Product{i}", Quantity = 1, UnitPrice = 1m });
            }

            var order = new Order
            {
                CustomerId = 1,
                CustomerEmail = "user@example.com",
                Region = "EU",
                Items = items
            };

            // Act
            var errors = processor.ValidateOrder(order);

            // Assert
            Assert.DoesNotContain("Order cannot contain more than 100 items.", errors);
        }

        [Fact]
        public void ValidateOrder_ItemsCountAboveMaxItems_AddsMaxItemsError()
        {
            // Arrange
            var processor = CreateProcessor();
            var items = new List<OrderItem>();
            for (int i = 0; i < 101; i++)
            {
                items.Add(new OrderItem { ProductName = $"Product{i}", Quantity = 1, UnitPrice = 1m });
            }

            var order = new Order
            {
                CustomerId = 1,
                CustomerEmail = "user@example.com",
                Region = "EU",
                Items = items
            };

            // Act
            var errors = processor.ValidateOrder(order);

            // Assert
            Assert.Contains("Order cannot contain more than 100 items.", errors);
        }

        [Fact]
        public void ValidateOrder_ItemWithBlankProductName_AddsProductNameError()
        {
            // Arrange
            var processor = CreateProcessor();
            var order = new Order
            {
                CustomerId = 1,
                CustomerEmail = "user@example.com",
                Region = "EU",
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductName = " ", Quantity = 1, UnitPrice = 10m }
                }
            };

            // Act
            var errors = processor.ValidateOrder(order);

            // Assert
            Assert.Contains("Item 1: product name is required.", errors);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void ValidateOrder_ItemWithNonPositiveQuantity_AddsQuantityError(int quantity)
        {
            // Arrange
            var processor = CreateProcessor();
            var order = new Order
            {
                CustomerId = 1,
                CustomerEmail = "user@example.com",
                Region = "EU",
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductName = "Product", Quantity = quantity, UnitPrice = 10m }
                }
            };

            // Act
            var errors = processor.ValidateOrder(order);

            // Assert
            Assert.Contains("Item 1: quantity must be positive.", errors);
        }

        [Fact]
        public void ValidateOrder_ItemWithNegativeUnitPrice_AddsUnitPriceError()
        {
            // Arrange
            var processor = CreateProcessor();
            var order = new Order
            {
                CustomerId = 1,
                CustomerEmail = "user@example.com",
                Region = "EU",
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductName = "Product", Quantity = 1, UnitPrice = -0.01m }
                }
            };

            // Act
            var errors = processor.ValidateOrder(order);

            // Assert
            Assert.Contains("Item 1: unit price cannot be negative.", errors);
        }

        [Fact]
        public void CalculateSubtotal_NullItems_ReturnsZero()
        {
            // Arrange
            var processor = CreateProcessor();

            // Act
            var subtotal = processor.CalculateSubtotal(null!);

            // Assert
            Assert.Equal(0m, subtotal);
        }

        [Fact]
        public void CalculateSubtotal_EmptyItems_ReturnsZero()
        {
            // Arrange
            var processor = CreateProcessor();

            // Act
            var subtotal = processor.CalculateSubtotal(new List<OrderItem>());

            // Assert
            Assert.Equal(0m, subtotal);
        }

        [Fact]
        public void CalculateSubtotal_AllInvalidItems_ReturnsZero()
        {
            // Arrange
            var processor = CreateProcessor();
            var items = new List<OrderItem>
            {
                new OrderItem { ProductName = "A", Quantity = 0, UnitPrice = 10m },
                new OrderItem { ProductName = "B", Quantity = 1, UnitPrice = -1m }
            };

            // Act
            var subtotal = processor.CalculateSubtotal(items);

            // Assert
            Assert.Equal(0m, subtotal);
        }

        [Fact]
        public void CalculateSubtotal_MixedValidAndInvalidItems_ReturnsOnlyValidItemTotals()
        {
            // Arrange
            var processor = CreateProcessor();
            var items = new List<OrderItem>
            {
                new OrderItem { ProductName = "Valid1", Quantity = 2, UnitPrice = 10m },
                new OrderItem { ProductName = "Invalid1", Quantity = 0, UnitPrice = 100m },
                new OrderItem { ProductName = "Valid2", Quantity = 1, UnitPrice = 5.5m }
            };

            // Act
            var subtotal = processor.CalculateSubtotal(items);

            // Assert
            Assert.Equal(25.5m, subtotal);
        }

        [Fact]
        public void CalculateSubtotal_QuantityExactlyTen_AppliesBulkDiscount()
        {
            // Arrange
            var processor = CreateProcessor();
            var items = new List<OrderItem>
            {
                new OrderItem { ProductName = "Bulk", Quantity = 10, UnitPrice = 10m }
            };

            // Act
            var subtotal = processor.CalculateSubtotal(items);

            // Assert
            Assert.Equal(95m, subtotal);
        }

        [Fact]
        public void CalculateSubtotal_RoundsToTwoDecimals_ReturnsRoundedSubtotal()
        {
            // Arrange
            var processor = CreateProcessor();
            var items = new List<OrderItem>
            {
                new OrderItem { ProductName = "A", Quantity = 1, UnitPrice = 10.005m }
            };

            // Act
            var subtotal = processor.CalculateSubtotal(items);

            // Assert
            Assert.Equal(10.00m, subtotal);
        }

        [Theory]
        [InlineData(-0.01, "Invalid")]
        [InlineData(0, "Invalid")]
        [InlineData(0.01, "Small")]
        [InlineData(49.99, "Small")]
        [InlineData(50, "Medium")]
        [InlineData(499.99, "Medium")]
        [InlineData(500, "Large")]
        [InlineData(4999.99, "Large")]
        [InlineData(5000, "Enterprise")]
        public void ClassifyOrder_BoundaryValues_ReturnsExpectedClassification(decimal total, string expected)
        {
            // Arrange
            var processor = CreateProcessor();

            // Act
            var classification = processor.ClassifyOrder(total);

            // Assert
            Assert.Equal(expected, classification);
        }

        [Fact]
        public void ProcessOrder_ValidationFails_ReturnsFalseWithoutCallingDependencies()
        {
            // Arrange
            var repository = new FakeOrderRepository();
            var pricing = new FakePricingService();
            var notification = new FakeNotificationService();
            var processor = CreateProcessor(repository, pricing, notification);
            var order = new Order
            {
                CustomerId = 0,
                CustomerEmail = "user@example.com",
                Region = "EU",
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductName = "Product", Quantity = 1, UnitPrice = 10m }
                }
            };

            // Act
            var result = processor.ProcessOrder(order);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ProcessOrder_SubtotalZero_ReturnsFalse()
        {
            // Arrange
            var repository = new FakeOrderRepository();
            var pricing = new FakePricingService();
            var notification = new FakeNotificationService();
            var processor = CreateProcessor(repository, pricing, notification);
            var order = new Order
            {
                CustomerId = 1,
                CustomerEmail = "user@example.com",
                Region = "EU",
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductName = "Invalid", Quantity = 0, UnitPrice = 10m }
                }
            };

            // Act
            var result = processor.ProcessOrder(order);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ProcessOrder_DiscountGreaterThanSubtotal_ClampsDiscountedSubtotalToZeroAndConfirmsOrder()
        {
            // Arrange
            var repository = new FakeOrderRepository();
            var pricing = new FakePricingService
            {
                DiscountToReturn = 15m,
                TaxRateToReturn = 0.10m
            };
            var notification = new FakeNotificationService();
            var processor = CreateProcessor(repository, pricing, notification);
            var order = new Order
            {
                Id = 7,
                CustomerId = 1,
                CustomerEmail = "user@example.com",
                Region = "EU",
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductName = "Product", Quantity = 1, UnitPrice = 10m }
                }
            };

            // Act
            var result = processor.ProcessOrder(order);

            // Assert
            Assert.True(result);
            Assert.Equal(0m, order.Tax);
        }

        [Fact]
        public void ProcessOrder_SuccessfulConfirmationPath_PopulatesOrderFieldsAndCallsDependenciesOnce()
        {
            // Arrange
            var repository = new FakeOrderRepository();
            var pricing = new FakePricingService
            {
                DiscountToReturn = 10m,
                TaxRateToReturn = 0.10m
            };
            var notification = new FakeNotificationService();
            var processor = CreateProcessor(repository, pricing, notification);
            var order = new Order
            {
                Id = 42,
                CustomerId = 1,
                CustomerEmail = "user@example.com",
                Region = "EU",
                CouponCode = "SAVE10",
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductName = "Product", Quantity = 2, UnitPrice = 50m }
                }
            };

            // Act
            var result = processor.ProcessOrder(order);

            // Assert
            Assert.True(result);
            Assert.Equal(OrderStatus.Confirmed, order.Status);
            Assert.Equal(100m, order.Subtotal);
            Assert.Equal(10m, order.Discount);
            Assert.Equal(9m, order.Tax);
            Assert.Equal(99m, order.Total);
        }

        [Fact]
        public void ProcessOrder_SuccessfulConfirmationPath_SavesOrderExactlyOnce()
        {
            // Arrange
            var repository = new FakeOrderRepository();
            var pricing = new FakePricingService
            {
                DiscountToReturn = 0m,
                TaxRateToReturn = 0.10m
            };
            var notification = new FakeNotificationService();
            var processor = CreateProcessor(repository, pricing, notification);
            var order = new Order
            {
                Id = 42,
                CustomerId = 1,
                CustomerEmail = "user@example.com",
                Region = "EU",
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductName = "Product", Quantity = 1, UnitPrice = 100m }
                }
            };

            // Act
            var result = processor.ProcessOrder(order);

            // Assert
            Assert.True(result);
            Assert.Equal(1, repository.SaveCallCount);
        }

        [Fact]
        public void ProcessOrder_SuccessfulConfirmationPath_SendsNotificationExactlyOnce()
        {
            // Arrange
            var repository = new FakeOrderRepository();
            var pricing = new FakePricingService
            {
                DiscountToReturn = 0m,
                TaxRateToReturn = 0.10m
            };
            var notification = new FakeNotificationService();
            var processor = CreateProcessor(repository, pricing, notification);
            var order = new Order
            {
                Id = 42,
                CustomerId = 1,
                CustomerEmail = "user@example.com",
                Region = "EU",
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductName = "Product", Quantity = 1, UnitPrice = 100m }
                }
            };

            // Act
            var result = processor.ProcessOrder(order);

            // Assert
            Assert.True(result);
            Assert.Equal(1, notification.SendCallCount);
        }

        [Fact]
        public void ProcessOrder_TotalExceedingMaxOrderTotal_ReturnsFalse()
        {
            // Arrange
            var repository = new FakeOrderRepository();
            var pricing = new FakePricingService
            {
                DiscountToReturn = 0m,
                TaxRateToReturn = 0.10m
            };
            var notification = new FakeNotificationService();
            var processor = CreateProcessor(repository, pricing, notification);
            var order = new Order
            {
                Id = 1,
                CustomerId = 1,
                CustomerEmail = "user@example.com",
                Region = "EU",
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductName = "Product", Quantity = 1, UnitPrice = 50000.01m }
                }
            };

            // Act
            var result = processor.ProcessOrder(order);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void CancelOrder_OrderNotFound_ReturnsFalse()
        {
            // Arrange
            var repository = new FakeOrderRepository { OrderToFind = null };
            var processor = CreateProcessor(repository);

            // Act
            var result = processor.CancelOrder(123);

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData(OrderStatus.Pending)]
        [InlineData(OrderStatus.Cancelled)]
        [InlineData(OrderStatus.Refunded)]
        public void CancelOrder_NonConfirmedOrder_ReturnsFalse(OrderStatus status)
        {
            // Arrange
            var repository = new FakeOrderRepository
            {
                OrderToFind = new Order { Id = 1, Status = status }
            };
            var processor = CreateProcessor(repository);

            // Act
            var result = processor.CancelOrder(1);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void CancelOrder_ConfirmedOrder_CancelsOrderAndSavesIt()
        {
            // Arrange
            var repository = new FakeOrderRepository
            {
                OrderToFind = new Order { Id = 1, Status = OrderStatus.Confirmed }
            };
            var processor = CreateProcessor(repository);

            // Act
            var result = processor.CancelOrder(1);

            // Assert
            Assert.True(result);
            Assert.Equal(OrderStatus.Cancelled, repository.OrderToFind!.Status);
            Assert.Equal(1, repository.SaveCallCount);
        }

        [Fact]
        public void GetCustomerTotalSpent_InvalidCustomerId_ReturnsZero()
        {
            // Arrange
            var processor = CreateProcessor();

            // Act
            var total = processor.GetCustomerTotalSpent(0);

            // Assert
            Assert.Equal(0m, total);
        }

        [Fact]
        public void GetCustomerTotalSpent_RepositoryReturnsNull_ReturnsZero()
        {
            // Arrange
            var repository = new FakeOrderRepository { OrdersByCustomer = null };
            var processor = CreateProcessor(repository);

            // Act
            var total = processor.GetCustomerTotalSpent(1);

            // Assert
            Assert.Equal(0m, total);
        }

        [Fact]
        public void GetCustomerTotalSpent_RepositoryReturnsEmptyList_ReturnsZero()
        {
            // Arrange
            var repository = new FakeOrderRepository { OrdersByCustomer = new List<Order>() };
            var processor = CreateProcessor(repository);

            // Act
            var total = processor.GetCustomerTotalSpent(1);

            // Assert
            Assert.Equal(0m, total);
        }

        [Fact]
        public void GetCustomerTotalSpent_MixedStatuses_OnlyConfirmedOrdersAreSummed()
        {
            // Arrange
            var repository = new FakeOrderRepository
            {
                OrdersByCustomer = new List<Order>
                {
                    new Order { Status = OrderStatus.Confirmed, Total = 10m },
                    new Order { Status = OrderStatus.Pending, Total = 20m },
                    new Order { Status = OrderStatus.Cancelled, Total = 30m },
                    new Order { Status = OrderStatus.Confirmed, Total = 40m }
                }
            };
            var processor = CreateProcessor(repository);

            // Act
            var total = processor.GetCustomerTotalSpent(1);

            // Assert
            Assert.Equal(50m, total);
        }

        [Fact]
        public void GetCustomerTotalSpent_VeryLargeConfirmedTotals_ReturnsFullSum()
        {
            // Arrange
            var repository = new FakeOrderRepository
            {
                OrdersByCustomer = new List<Order>
                {
                    new Order { Status = OrderStatus.Confirmed, Total = 25000m },
                    new Order { Status = OrderStatus.Confirmed, Total = 25000m }
                }
            };
            var processor = CreateProcessor(repository);

            // Act
            var total = processor.GetCustomerTotalSpent(1);

            // Assert
            Assert.Equal(50000m, total);
        }
    }

    public class OrderProcessorIntegrationTests
    {
        [Fact]
        public void CalculateSubtotal_NullItems_ReturnsZeroAndDoesNotInteractWithDependencies()
        {
            // Arrange
            var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
            var pricingMock = new Mock<IPricingService>(MockBehavior.Strict);
            var notificationMock = new Mock<INotificationService>(MockBehavior.Strict);
            var sut = new OrderProcessor(repositoryMock.Object, pricingMock.Object, notificationMock.Object);

            // Act
            var result = sut.CalculateSubtotal(null);

            // Assert
            Assert.Equal(0m, result);
            repositoryMock.VerifyNoOtherCalls();
            pricingMock.VerifyNoOtherCalls();
            notificationMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void CalculateSubtotal_MixedValidAndInvalidItems_ReturnsRoundedSubtotalForValidItemsOnly()
        {
            // Arrange
            var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
            var pricingMock = new Mock<IPricingService>(MockBehavior.Strict);
            var notificationMock = new Mock<INotificationService>(MockBehavior.Strict);
            var sut = new OrderProcessor(repositoryMock.Object, pricingMock.Object, notificationMock.Object);

            var items = new List<OrderItem>
            {
                new OrderItem { ProductName = "Valid Small", Quantity = 2, UnitPrice = 10.00m },
                new OrderItem { ProductName = "Invalid Qty", Quantity = 0, UnitPrice = 99.99m },
                new OrderItem { ProductName = "Bulk Discount", Quantity = 10, UnitPrice = 5.00m },
                new OrderItem { ProductName = "Invalid Price", Quantity = 1, UnitPrice = -1.00m },
                new OrderItem { ProductName = "Rounding Item", Quantity = 3, UnitPrice = 3.3333m }
            };

            // Act
            var result = sut.CalculateSubtotal(items);

            // Assert
            Assert.Equal(77.50m, result);
            repositoryMock.VerifyNoOtherCalls();
            pricingMock.VerifyNoOtherCalls();
            notificationMock.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData(9, 10.00, 90.00)]
        [InlineData(10, 10.00, 95.00)]
        [InlineData(10, 0.3333, 3.17)]
        public void CalculateSubtotal_BulkDiscountAndRounding_BehavesCorrectly(int quantity, decimal unitPrice, decimal expectedSubtotal)
        {
            // Arrange
            var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
            var pricingMock = new Mock<IPricingService>(MockBehavior.Strict);
            var notificationMock = new Mock<INotificationService>(MockBehavior.Strict);
            var sut = new OrderProcessor(repositoryMock.Object, pricingMock.Object, notificationMock.Object);

            var items = new List<OrderItem>
            {
                new OrderItem { ProductName = "Item", Quantity = quantity, UnitPrice = unitPrice }
            };

            // Act
            var result = sut.CalculateSubtotal(items);

            // Assert
            Assert.Equal(expectedSubtotal, result);
            repositoryMock.VerifyNoOtherCalls();
            pricingMock.VerifyNoOtherCalls();
            notificationMock.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData(-1, "Invalid")]
        [InlineData(0, "Invalid")]
        [InlineData(0.01, "Small")]
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
            var pricingMock = new Mock<IPricingService>(MockBehavior.Strict);
            var notificationMock = new Mock<INotificationService>(MockBehavior.Strict);
            var sut = new OrderProcessor(repositoryMock.Object, pricingMock.Object, notificationMock.Object);

            // Act
            var result = sut.ClassifyOrder(total);

            // Assert
            Assert.Equal(expected, result);
            repositoryMock.VerifyNoOtherCalls();
            pricingMock.VerifyNoOtherCalls();
            notificationMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void ValidateOrder_NullOrder_ReturnsSingleErrorAndDoesNotCallDependencies()
        {
            // Arrange
            var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
            var pricingMock = new Mock<IPricingService>(MockBehavior.Strict);
            var notificationMock = new Mock<INotificationService>(MockBehavior.Strict);
            var sut = new OrderProcessor(repositoryMock.Object, pricingMock.Object, notificationMock.Object);

            // Act
            var errors = sut.ValidateOrder(null);

            // Assert
            Assert.Single(errors);
            Assert.Equal("Order cannot be null.", errors[0]);
            repositoryMock.VerifyNoOtherCalls();
            pricingMock.VerifyNoOtherCalls();
            notificationMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void ValidateOrder_InvalidCustomerAndItems_ReturnsAllExpectedErrors()
        {
            // Arrange
            var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
            var pricingMock = new Mock<IPricingService>(MockBehavior.Strict);
            var notificationMock = new Mock<INotificationService>(MockBehavior.Strict);
            var sut = new OrderProcessor(repositoryMock.Object, pricingMock.Object, notificationMock.Object);

            var order = new Order
            {
                CustomerId = 0,
                CustomerEmail = "invalid-email",
                Region = "",
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductName = "", Quantity = 0, UnitPrice = -1m }
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
            pricingMock.VerifyNoOtherCalls();
            notificationMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void ValidateOrder_ItemCountExactlyOneHundred_IsAccepted()
        {
            // Arrange
            var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
            var pricingMock = new Mock<IPricingService>(MockBehavior.Strict);
            var notificationMock = new Mock<INotificationService>(MockBehavior.Strict);
            var sut = new OrderProcessor(repositoryMock.Object, pricingMock.Object, notificationMock.Object);

            var items = new List<OrderItem>();
            for (int i = 0; i < 100; i++)
            {
                items.Add(new OrderItem { ProductName = $"Product {i + 1}", Quantity = 1, UnitPrice = 1m });
            }

            var order = new Order
            {
                CustomerId = 123,
                CustomerEmail = "customer@example.com",
                Region = "US",
                Items = items
            };

            // Act
            var errors = sut.ValidateOrder(order);

            // Assert
            Assert.Empty(errors);
            repositoryMock.VerifyNoOtherCalls();
            pricingMock.VerifyNoOtherCalls();
            notificationMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void ValidateOrder_ItemCountGreaterThanOneHundred_ReturnsLimitError()
        {
            // Arrange
            var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
            var pricingMock = new Mock<IPricingService>(MockBehavior.Strict);
            var notificationMock = new Mock<INotificationService>(MockBehavior.Strict);
            var sut = new OrderProcessor(repositoryMock.Object, pricingMock.Object, notificationMock.Object);

            var items = new List<OrderItem>();
            for (int i = 0; i < 101; i++)
            {
                items.Add(new OrderItem { ProductName = $"Product {i + 1}", Quantity = 1, UnitPrice = 1m });
            }

            var order = new Order
            {
                CustomerId = 123,
                CustomerEmail = "customer@example.com",
                Region = "US",
                Items = items
            };

            // Act
            var errors = sut.ValidateOrder(order);

            // Assert
            Assert.Single(errors);
            Assert.Equal("Order cannot contain more than 100 items.", errors[0]);
            repositoryMock.VerifyNoOtherCalls();
            pricingMock.VerifyNoOtherCalls();
            notificationMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void ProcessOrder_ValidationFails_ReturnsFalseAndDoesNotCallDownstreamDependencies()
        {
            // Arrange
            var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
            var pricingMock = new Mock<IPricingService>(MockBehavior.Strict);
            var notificationMock = new Mock<INotificationService>(MockBehavior.Strict);
            var sut = new OrderProcessor(repositoryMock.Object, pricingMock.Object, notificationMock.Object);

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
            Assert.Equal(OrderStatus.Pending, order.Status);
            repositoryMock.Verify(r => r.Save(It.IsAny<Order>()), Times.Never);
            repositoryMock.Verify(r => r.FindById(It.IsAny<int>()), Times.Never);
            repositoryMock.Verify(r => r.GetByCustomerId(It.IsAny<int>()), Times.Never);
            pricingMock.Verify(p => p.CalculateDiscount(It.IsAny<decimal>(), It.IsAny<string?>()), Times.Never);
            pricingMock.Verify(p => p.GetTaxRate(It.IsAny<string>()), Times.Never);
            notificationMock.Verify(n => n.SendOrderConfirmation(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<decimal>()), Times.Never);
            repositoryMock.VerifyNoOtherCalls();
            pricingMock.VerifyNoOtherCalls();
            notificationMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void ProcessOrder_SubtotalIsZero_ReturnsFalseAndDoesNotCallPricingRepositoryOrNotification()
        {
            // Arrange
            var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
            var pricingMock = new Mock<IPricingService>(MockBehavior.Strict);
            var notificationMock = new Mock<INotificationService>(MockBehavior.Strict);
            var sut = new OrderProcessor(repositoryMock.Object, pricingMock.Object, notificationMock.Object);

            var order = new Order
            {
                Id = 10,
                CustomerId = 123,
                CustomerEmail = "customer@example.com",
                Region = "US",
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
            pricingMock.Verify(p => p.CalculateDiscount(It.IsAny<decimal>(), It.IsAny<string?>()), Times.Never);
            pricingMock.Verify(p => p.GetTaxRate(It.IsAny<string>()), Times.Never);
            notificationMock.Verify(n => n.SendOrderConfirmation(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<decimal>()), Times.Never);
            repositoryMock.VerifyNoOtherCalls();
            pricingMock.VerifyNoOtherCalls();
            notificationMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void ProcessOrder_DiscountGreaterThanSubtotal_ClampsDiscountedSubtotalAndSavesConfirmedOrder()
        {
            // Arrange
            var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
            var pricingMock = new Mock<IPricingService>(MockBehavior.Strict);
            var notificationMock = new Mock<INotificationService>(MockBehavior.Strict);
            var sut = new OrderProcessor(repositoryMock.Object, pricingMock.Object, notificationMock.Object);

            var order = new Order
            {
                Id = 77,
                CustomerId = 123,
                CustomerEmail = "customer@example.com",
                Region = "EU",
                CouponCode = "BIGSALE",
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductName = "Premium", Quantity = 10, UnitPrice = 100m }
                }
            };

            pricingMock.Setup(p => p.CalculateDiscount(950m, "BIGSALE")).Returns(1000m);
            pricingMock.Setup(p => p.GetTaxRate("EU")).Returns(0.20m);
            repositoryMock.Setup(r => r.Save(It.Is<Order>(o =>
                o.Id == 77 &&
                o.CustomerId == 123 &&
                o.CustomerEmail == "customer@example.com" &&
                o.Region == "EU" &&
                o.Subtotal == 950m &&
                o.Discount == 1000m &&
                o.Tax == 0m &&
                o.Total == 0m &&
                o.Status == OrderStatus.Confirmed &&
                o.CreatedAt != default &&
                o.CompletedAt == null
            )));
            notificationMock.Setup(n => n.SendOrderConfirmation("customer@example.com", 77, 0m));

            // Act
            var result = sut.ProcessOrder(order);

            // Assert
            Assert.True(result);
            Assert.Equal(950m, order.Subtotal);
            Assert.Equal(1000m, order.Discount);
            Assert.Equal(0m, order.Tax);
            Assert.Equal(0m, order.Total);
            Assert.Equal(OrderStatus.Confirmed, order.Status);
            Assert.NotEqual(default, order.CreatedAt);

            pricingMock.Verify(p => p.CalculateDiscount(It.Is<decimal>(s => s == 950m), It.Is<string?>(c => c == "BIGSALE")), Times.Once);
            pricingMock.Verify(p => p.GetTaxRate(It.Is<string>(r => r == "EU")), Times.Once);
            repositoryMock.Verify(r => r.Save(It.Is<Order>(o =>
                o.Subtotal == 950m &&
                o.Discount == 1000m &&
                o.Tax == 0m &&
                o.Total == 0m &&
                o.Status == OrderStatus.Confirmed &&
                o.CustomerEmail == "customer@example.com" &&
                o.Id == 77)), Times.Once);
            notificationMock.Verify(n => n.SendOrderConfirmation(
                It.Is<string>(e => e == "customer@example.com"),
                It.Is<int>(id => id == 77),
                It.Is<decimal>(t => t == 0m)), Times.Once);
            repositoryMock.Verify(r => r.FindById(It.IsAny<int>()), Times.Never);
            repositoryMock.Verify(r => r.GetByCustomerId(It.IsAny<int>()), Times.Never);
            pricingMock.VerifyNoOtherCalls();
            repositoryMock.VerifyNoOtherCalls();
            notificationMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void ProcessOrder_SuccessfulConfirmationPath_CallsPricingRepositoryAndNotificationOnce()
        {
            // Arrange
            var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
            var pricingMock = new Mock<IPricingService>(MockBehavior.Strict);
            var notificationMock = new Mock<INotificationService>(MockBehavior.Strict);
            var sut = new OrderProcessor(repositoryMock.Object, pricingMock.Object, notificationMock.Object);

            var order = new Order
            {
                Id = 15,
                CustomerId = 456,
                CustomerEmail = "buyer@contoso.com",
                Region = "US",
                CouponCode = null,
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductName = "Item 1", Quantity = 2, UnitPrice = 25m },
                    new OrderItem { ProductName = "Item 2", Quantity = 1, UnitPrice = 10m }
                }
            };

            pricingMock.Setup(p => p.CalculateDiscount(60m, null)).Returns(5m);
            pricingMock.Setup(p => p.GetTaxRate("US")).Returns(0.10m);
            repositoryMock.Setup(r => r.Save(It.Is<Order>(o =>
                o.Id == 15 &&
                o.CustomerId == 456 &&
                o.CustomerEmail == "buyer@contoso.com" &&
                o.Region == "US" &&
                o.Subtotal == 60m &&
                o.Discount == 5m &&
                o.Tax == 5.5m &&
                o.Total == 60.5m &&
                o.Status == OrderStatus.Confirmed &&
                o.CreatedAt != default)));
            notificationMock.Setup(n => n.SendOrderConfirmation("buyer@contoso.com", 15, 60.5m));

            // Act
            var result = sut.ProcessOrder(order);

            // Assert
            Assert.True(result);
            Assert.Equal(60m, order.Subtotal);
            Assert.Equal(5m, order.Discount);
            Assert.Equal(5.5m, order.Tax);
            Assert.Equal(60.5m, order.Total);
            Assert.Equal(OrderStatus.Confirmed, order.Status);
            Assert.NotEqual(default, order.CreatedAt);

            pricingMock.Verify(p => p.CalculateDiscount(It.Is<decimal>(subtotal => subtotal == 60m), It.Is<string?>(code => code == null)), Times.Once);
            pricingMock.Verify(p => p.GetTaxRate(It.Is<string>(region => region == "US")), Times.Once);
            repositoryMock.Verify(r => r.Save(It.Is<Order>(o =>
                o.Subtotal == 60m &&
                o.Discount == 5m &&
                o.Tax == 5.5m &&
                o.Total == 60.5m &&
                o.CustomerEmail == "buyer@contoso.com" &&
                o.Status == OrderStatus.Confirmed)), Times.Once);
            notificationMock.Verify(n => n.SendOrderConfirmation(
                It.Is<string>(email => email == "buyer@contoso.com"),
                It.Is<int>(id => id == 15),
                It.Is<decimal>(total => total == 60.5m)), Times.Once);
            repositoryMock.VerifyNoOtherCalls();
            pricingMock.VerifyNoOtherCalls();
            notificationMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void CancelOrder_OrderNotFound_ReturnsFalseAndDoesNotSaveOrNotify()
        {
            // Arrange
            var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
            var pricingMock = new Mock<IPricingService>(MockBehavior.Strict);
            var notificationMock = new Mock<INotificationService>(MockBehavior.Strict);
            var sut = new OrderProcessor(repositoryMock.Object, pricingMock.Object, notificationMock.Object);

            repositoryMock.Setup(r => r.FindById(999)).Returns((Order?)null);

            // Act
            var result = sut.CancelOrder(999);

            // Assert
            Assert.False(result);
            repositoryMock.Verify(r => r.FindById(It.Is<int>(id => id == 999)), Times.Once);
            repositoryMock.Verify(r => r.Save(It.IsAny<Order>()), Times.Never);
            pricingMock.Verify(p => p.CalculateDiscount(It.IsAny<decimal>(), It.IsAny<string?>()), Times.Never);
            pricingMock.Verify(p => p.GetTaxRate(It.IsAny<string>()), Times.Never);
            notificationMock.Verify(n => n.SendOrderConfirmation(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<decimal>()), Times.Never);
            repositoryMock.VerifyNoOtherCalls();
            pricingMock.VerifyNoOtherCalls();
            notificationMock.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData(OrderStatus.Pending)]
        [InlineData(OrderStatus.Cancelled)]
        [InlineData(OrderStatus.Refunded)]
        public void CancelOrder_NonConfirmedOrder_ReturnsFalseAndDoesNotPersistChange(OrderStatus initialStatus)
        {
            // Arrange
            var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
            var pricingMock = new Mock<IPricingService>(MockBehavior.Strict);
            var notificationMock = new Mock<INotificationService>(MockBehavior.Strict);
            var sut = new OrderProcessor(repositoryMock.Object, pricingMock.Object, notificationMock.Object);

            var order = new Order { Id = 5, Status = initialStatus };
            repositoryMock.Setup(r => r.FindById(5)).Returns(order);

            // Act
            var result = sut.CancelOrder(5);

            // Assert
            Assert.False(result);
            Assert.Equal(initialStatus, order.Status);
            repositoryMock.Verify(r => r.FindById(It.Is<int>(id => id == 5)), Times.Once);
            repositoryMock.Verify(r => r.Save(It.IsAny<Order>()), Times.Never);
            pricingMock.Verify(p => p.CalculateDiscount(It.IsAny<decimal>(), It.IsAny<string?>()), Times.Never);
            pricingMock.Verify(p => p.GetTaxRate(It.IsAny<string>()), Times.Never);
            notificationMock.Verify(n => n.SendOrderConfirmation(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<decimal>()), Times.Never);
            repositoryMock.VerifyNoOtherCalls();
            pricingMock.VerifyNoOtherCalls();
            notificationMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void CancelOrder_ConfirmedOrder_CancelsAndSavesOnce()
        {
            // Arrange
            var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
            var pricingMock = new Mock<IPricingService>(MockBehavior.Strict);
            var notificationMock = new Mock<INotificationService>(MockBehavior.Strict);
            var sut = new OrderProcessor(repositoryMock.Object, pricingMock.Object, notificationMock.Object);

            var order = new Order { Id = 42, Status = OrderStatus.Confirmed };
            repositoryMock.Setup(r => r.FindById(42)).Returns(order);
            repositoryMock.Setup(r => r.Save(It.Is<Order>(o => o.Id == 42 && o.Status == OrderStatus.Cancelled)));

            // Act
            var result = sut.CancelOrder(42);

            // Assert
            Assert.True(result);
            Assert.Equal(OrderStatus.Cancelled, order.Status);
            repositoryMock.Verify(r => r.FindById(It.Is<int>(id => id == 42)), Times.Once);
            repositoryMock.Verify(r => r.Save(It.Is<Order>(o =>
                o.Id == 42 &&
                o.Status == OrderStatus.Cancelled)), Times.Once);
            pricingMock.Verify(p => p.CalculateDiscount(It.IsAny<decimal>(), It.IsAny<string?>()), Times.Never);
            pricingMock.Verify(p => p.GetTaxRate(It.IsAny<string>()), Times.Never);
            notificationMock.Verify(n => n.SendOrderConfirmation(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<decimal>()), Times.Never);
            repositoryMock.VerifyNoOtherCalls();
            pricingMock.VerifyNoOtherCalls();
            notificationMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void GetCustomerTotalSpent_CustomerIdLessThanOrEqualZero_ReturnsZeroAndDoesNotQueryRepository()
        {
            // Arrange
            var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
            var pricingMock = new Mock<IPricingService>(MockBehavior.Strict);
            var notificationMock = new Mock<INotificationService>(MockBehavior.Strict);
            var sut = new OrderProcessor(repositoryMock.Object, pricingMock.Object, notificationMock.Object);

            // Act
            var result = sut.GetCustomerTotalSpent(0);

            // Assert
            Assert.Equal(0m, result);
            repositoryMock.Verify(r => r.GetByCustomerId(It.IsAny<int>()), Times.Never);
            repositoryMock.Verify(r => r.FindById(It.IsAny<int>()), Times.Never);
            repositoryMock.Verify(r => r.Save(It.IsAny<Order>()), Times.Never);
            pricingMock.Verify(p => p.CalculateDiscount(It.IsAny<decimal>(), It.IsAny<string?>()), Times.Never);
            pricingMock.Verify(p => p.GetTaxRate(It.IsAny<string>()), Times.Never);
            notificationMock.Verify(n => n.SendOrderConfirmation(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<decimal>()), Times.Never);
            repositoryMock.VerifyNoOtherCalls();
            pricingMock.VerifyNoOtherCalls();
            notificationMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void GetCustomerTotalSpent_MixedStatuses_AggregatesConfirmedOrdersOnly()
        {
            // Arrange
            var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
            var pricingMock = new Mock<IPricingService>(MockBehavior.Strict);
            var notificationMock = new Mock<INotificationService>(MockBehavior.Strict);
            var sut = new OrderProcessor(repositoryMock.Object, pricingMock.Object, notificationMock.Object);

            var orders = new List<Order>
            {
                new Order { Status = OrderStatus.Confirmed, Total = 100m },
                new Order { Status = OrderStatus.Pending, Total = 999m },
                new Order { Status = OrderStatus.Cancelled, Total = 50m },
                new Order { Status = OrderStatus.Confirmed, Total = 25.75m },
                new Order { Status = OrderStatus.Refunded, Total = 12m }
            };

            repositoryMock.Setup(r => r.GetByCustomerId(123)).Returns(orders);

            // Act
            var result = sut.GetCustomerTotalSpent(123);

            // Assert
            Assert.Equal(125.75m, result);
            repositoryMock.Verify(r => r.GetByCustomerId(It.Is<int>(id => id == 123)), Times.Once);
            repositoryMock.Verify(r => r.FindById(It.IsAny<int>()), Times.Never);
            repositoryMock.Verify(r => r.Save(It.IsAny<Order>()), Times.Never);
            pricingMock.Verify(p => p.CalculateDiscount(It.IsAny<decimal>(), It.IsAny<string?>()), Times.Never);
            pricingMock.Verify(p => p.GetTaxRate(It.IsAny<string>()), Times.Never);
            notificationMock.Verify(n => n.SendOrderConfirmation(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<decimal>()), Times.Never);
            repositoryMock.VerifyNoOtherCalls();
            pricingMock.VerifyNoOtherCalls();
            notificationMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void GetCustomerTotalSpent_RepositoryReturnsNull_ReturnsZero()
        {
            // Arrange
            var repositoryMock = new Mock<IOrderRepository>(MockBehavior.Strict);
            var pricingMock = new Mock<IPricingService>(MockBehavior.Strict);
            var notificationMock = new Mock<INotificationService>(MockBehavior.Strict);
            var sut = new OrderProcessor(repositoryMock.Object, pricingMock.Object, notificationMock.Object);

            repositoryMock.Setup(r => r.GetByCustomerId(321)).Returns((List<Order>?)null);

            // Act
            var result = sut.GetCustomerTotalSpent(321);

            // Assert
            Assert.Equal(0m, result);
            repositoryMock.Verify(r => r.GetByCustomerId(It.Is<int>(id => id == 321)), Times.Once);
            repositoryMock.Verify(r => r.FindById(It.IsAny<int>()), Times.Never);
            repositoryMock.Verify(r => r.Save(It.IsAny<Order>()), Times.Never);
            pricingMock.Verify(p => p.CalculateDiscount(It.IsAny<decimal>(), It.IsAny<string?>()), Times.Never);
            pricingMock.Verify(p => p.GetTaxRate(It.IsAny<string>()), Times.Never);
            notificationMock.Verify(n => n.SendOrderConfirmation(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<decimal>()), Times.Never);
            repositoryMock.VerifyNoOtherCalls();
            pricingMock.VerifyNoOtherCalls();
            notificationMock.VerifyNoOtherCalls();
        }
    }
}