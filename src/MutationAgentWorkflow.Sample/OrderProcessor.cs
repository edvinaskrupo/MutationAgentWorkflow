namespace MutationAgentWorkflow.Sample;

public interface IOrderRepository
{
    Order? FindById(int orderId);
    void Save(Order order);
    List<Order> GetByCustomerId(int customerId);
}

public interface IPricingService
{
    decimal CalculateDiscount(decimal subtotal, string? couponCode);
    decimal GetTaxRate(string region);
}

public interface INotificationService
{
    void SendOrderConfirmation(string email, int orderId, decimal total);
}

public class Order
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public List<OrderItem> Items { get; set; } = new();
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public string? CouponCode { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class OrderItem
{
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public enum OrderStatus
{
    Pending,
    Confirmed,
    Cancelled,
    Refunded
}

/// <summary>
/// Complex service with 3 injected dependencies, mixed standalone logic
/// and dependency-calling methods. Designed as Experiment 3 for the thesis:
/// triggers "Both" strategy (unit + integration tests).
/// </summary>
public class OrderProcessor
{
    private readonly IOrderRepository _repository;
    private readonly IPricingService _pricingService;
    private readonly INotificationService _notificationService;

    private const decimal MaxOrderTotal = 50_000m;
    private const int MaxItemsPerOrder = 100;

    public OrderProcessor(
        IOrderRepository repository,
        IPricingService pricingService,
        INotificationService notificationService)
    {
        _repository = repository;
        _pricingService = pricingService;
        _notificationService = notificationService;
    }

    public decimal CalculateSubtotal(List<OrderItem> items)
    {
        if (items == null || items.Count == 0)
            return 0m;

        decimal subtotal = 0m;
        foreach (var item in items)
        {
            if (item.Quantity <= 0 || item.UnitPrice < 0)
                continue;

            decimal lineTotal = item.Quantity * item.UnitPrice;

            if (item.Quantity >= 10)
                lineTotal *= 0.95m;

            subtotal += lineTotal;
        }

        return Math.Round(subtotal, 2);
    }

    public string ClassifyOrder(decimal total)
    {
        return total switch
        {
            <= 0 => "Invalid",
            < 50 => "Small",
            < 500 => "Medium",
            < 5000 => "Large",
            _ => "Enterprise"
        };
    }

    public List<string> ValidateOrder(Order order)
    {
        var errors = new List<string>();

        if (order == null)
        {
            errors.Add("Order cannot be null.");
            return errors;
        }

        if (order.CustomerId <= 0)
            errors.Add("Customer ID must be positive.");

        if (string.IsNullOrWhiteSpace(order.CustomerEmail))
            errors.Add("Customer email is required.");
        else if (!order.CustomerEmail.Contains('@'))
            errors.Add("Customer email must be valid.");

        if (string.IsNullOrWhiteSpace(order.Region))
            errors.Add("Region is required.");

        if (order.Items == null || order.Items.Count == 0)
            errors.Add("Order must contain at least one item.");
        else if (order.Items.Count > MaxItemsPerOrder)
            errors.Add($"Order cannot contain more than {MaxItemsPerOrder} items.");
        else
        {
            for (int i = 0; i < order.Items.Count; i++)
            {
                var item = order.Items[i];
                if (string.IsNullOrWhiteSpace(item.ProductName))
                    errors.Add($"Item {i + 1}: product name is required.");
                if (item.Quantity <= 0)
                    errors.Add($"Item {i + 1}: quantity must be positive.");
                if (item.UnitPrice < 0)
                    errors.Add($"Item {i + 1}: unit price cannot be negative.");
            }
        }

        return errors;
    }

    public bool ProcessOrder(Order order)
    {
        var validationErrors = ValidateOrder(order);
        if (validationErrors.Count > 0)
            return false;

        order.Subtotal = CalculateSubtotal(order.Items);

        if (order.Subtotal <= 0)
            return false;

        order.Discount = _pricingService.CalculateDiscount(order.Subtotal, order.CouponCode);
        var discountedSubtotal = order.Subtotal - order.Discount;

        if (discountedSubtotal < 0)
            discountedSubtotal = 0;

        var taxRate = _pricingService.GetTaxRate(order.Region);
        order.Tax = Math.Round(discountedSubtotal * taxRate, 2);
        order.Total = Math.Round(discountedSubtotal + order.Tax, 2);

        if (order.Total > MaxOrderTotal)
            return false;

        order.Status = OrderStatus.Confirmed;
        order.CreatedAt = DateTime.UtcNow;
        _repository.Save(order);

        _notificationService.SendOrderConfirmation(
            order.CustomerEmail, order.Id, order.Total);

        return true;
    }

    public bool CancelOrder(int orderId)
    {
        var order = _repository.FindById(orderId);

        if (order == null)
            return false;

        if (order.Status != OrderStatus.Confirmed)
            return false;

        order.Status = OrderStatus.Cancelled;
        _repository.Save(order);
        return true;
    }

    public decimal GetCustomerTotalSpent(int customerId)
    {
        if (customerId <= 0)
            return 0m;

        var orders = _repository.GetByCustomerId(customerId);

        if (orders == null || orders.Count == 0)
            return 0m;

        decimal total = 0m;
        foreach (var order in orders)
        {
            if (order.Status == OrderStatus.Confirmed)
                total += order.Total;
        }

        return total;
    }
}
