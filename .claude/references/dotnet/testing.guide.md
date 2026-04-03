# .NET 測試規範

本專案的單元測試規範與最佳實踐。

---

## A. 本專案測試技術棧

| Tool | Purpose | Note |
|------|---------|------|
| **xUnit** | 測試框架 | `[Fact]`, `[Theory]` |
| **NSubstitute** | Mocking | `Substitute.For<T>()` |
| **FluentAssertions** | Assertions | `.Should().BeTrue()` (版本 < 8.0.0) |
| **Reqnroll** | BDD 整合測試 | Gherkin 語法 |

---

## B. Test Naming Conventions

### Pattern: MethodName_WhenCondition_ShouldExpectedResult

```csharp
[Fact]
public async Task GetOrderAsync_WhenOrderExists_ShouldReturnOrder()
{
    // ...
}

[Fact]
public async Task GetOrderAsync_WhenOrderNotFound_ShouldReturnFailure()
{
    // ...
}

[Fact]
public void CalculateTotal_WhenCartIsEmpty_ShouldReturnZero()
{
    // ...
}
```

---

## C. Test Structure (AAA Pattern)

### 搭配 Result Pattern

```csharp
[Fact]
public async Task GetOrderAsync_WhenOrderExists_ShouldReturnSuccessResult()
{
    // Arrange
    const int orderId = 1;
    var expectedOrder = new Order { Id = orderId, Total = 100 };
    _orderRepository.GetByIdAsync(orderId, Arg.Any<CancellationToken>())
        .Returns(Result.Success<Order, Failure>(expectedOrder));
    
    // Act
    var result = await _service.GetOrderAsync(orderId, CancellationToken.None);
    
    // Assert
    result.IsSuccess.Should().BeTrue();
    result.Value.Should().NotBeNull();
    result.Value.Id.Should().Be(orderId);
    result.Value.Total.Should().Be(100);
}

[Fact]
public async Task GetOrderAsync_WhenOrderNotFound_ShouldReturnFailure()
{
    // Arrange
    const int invalidId = -1;
    _orderRepository.GetByIdAsync(invalidId, Arg.Any<CancellationToken>())
        .Returns(FailureProvider.CreateFailure(ErrorCode.NotFound));
    
    // Act
    var result = await _service.GetOrderAsync(invalidId, CancellationToken.None);
    
    // Assert
    result.IsFailure.Should().BeTrue();
    result.Error.Code.Should().Be(ErrorCode.NotFound);
}
```

---

## D. Test Organization

### 專案結構

```
src/be/
├── JobBank1111.Event.Test/              # Unit Tests
│   └── Events/
│       └── [Domain]/
│           └── Services/
│               └── [Name]ServiceTests.cs
│
└── JobBank1111.Event.IntegrationTest/   # Integration Tests (Reqnroll BDD)
    └── Features/
        └── [Feature].feature
```

### Test Class 組織

```csharp
public class OrderServiceTests
{
    // 使用 NSubstitute 建立 Mock
    private readonly IOrderRepository _orderRepository = Substitute.For<IOrderRepository>();
    private readonly IFailureProvider _failureProvider = Substitute.For<IFailureProvider>();
    private readonly ILogger<OrderService> _logger = Substitute.For<ILogger<OrderService>>();
    private readonly OrderService _service;
    
    public OrderServiceTests()
    {
        _service = new OrderService(
            _orderRepository,
            _failureProvider,
            _logger);
    }
    
    [Fact]
    public async Task GetOrderAsync_WhenOrderExists_ShouldReturnOrder()
    {
        // ...
    }
    
    [Fact]
    public async Task GetOrderAsync_WhenOrderNotFound_ShouldReturnFailure()
    {
        // ...
    }
}
```

---

## E. NSubstitute 用法

### 基本 Setup

```csharp
// 回傳值設定
_orderRepository.GetByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
    .Returns(new Order { Id = 1 });

// 特定參數
_orderRepository.GetByIdAsync(1, Arg.Any<CancellationToken>())
    .Returns(new Order { Id = 1 });

// 回傳 Result
_orderRepository.GetByIdAsync(1, Arg.Any<CancellationToken>())
    .Returns(Result.Success<Order, Failure>(new Order { Id = 1 }));

// 回傳 Failure
_orderRepository.GetByIdAsync(-1, Arg.Any<CancellationToken>())
    .Returns(FailureProvider.CreateFailure(ErrorCode.NotFound));

// 拋出例外
_orderRepository.GetByIdAsync(-1, Arg.Any<CancellationToken>())
    .ThrowsAsync(new DbException("Connection failed"));
```

### 驗證呼叫

```csharp
// 驗證被呼叫
await _orderRepository.Received().SaveAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());

// 驗證沒有被呼叫
await _orderRepository.DidNotReceive().DeleteAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());

// 驗證呼叫次數
await _orderRepository.Received(1).GetByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
```

---

## F. FluentAssertions 用法

```csharp
// 基本 Assertions
result.Should().NotBeNull();
result.Should().BeTrue();
result.Should().Be(expectedValue);

// 集合 Assertions
items.Should().NotBeEmpty();
items.Should().HaveCount(5);
items.Should().Contain(x => x.Id == 1);
items.Should().AllSatisfy(item =>
{
    item.Name.Should().NotBeNullOrEmpty();
    item.Status.Should().Be(Status.Active);
});

// Result Pattern Assertions
result.IsSuccess.Should().BeTrue();
result.IsFailure.Should().BeTrue();
result.Value.Should().NotBeNull();
result.Error.Code.Should().Be(ErrorCode.NotFound);

// 字串 Assertions
name.Should().NotBeNullOrEmpty();
name.Should().StartWith("Order");
name.Should().Contain("123");
```

> ⚠️ **注意**：FluentAssertions 版本請使用 < 8.0.0

---

## G. 何時該 Mock

| Mock | Don't Mock |
|------|------------|
| Repository（外部依賴） | 被測試的類別本身 |
| HttpClient / API 呼叫 | 簡單的 Value Objects |
| 時間相關操作 | Pure functions |
| 檔案系統操作 | 靜態工具方法 |
| ILogger | - |

---

## H. Test Data Builders

```csharp
public class OrderBuilder
{
    private int _id = 1;
    private decimal _total = 100;
    private OrderStatus _status = OrderStatus.Pending;
    private List<OrderItem> _items = new();
    
    public OrderBuilder WithId(int id)
    {
        _id = id;
        return this;
    }
    
    public OrderBuilder WithTotal(decimal total)
    {
        _total = total;
        return this;
    }
    
    public OrderBuilder WithStatus(OrderStatus status)
    {
        _status = status;
        return this;
    }
    
    public Order Build() => new()
    {
        Id = _id,
        Total = _total,
        Status = _status,
        Items = _items,
    };
}

// 使用
var order = new OrderBuilder()
    .WithId(123)
    .WithStatus(OrderStatus.Completed)
    .Build();
```

---

## I. 整合測試（Reqnroll BDD）

```gherkin
# OrderFeature.feature
Feature: Order Management

Scenario: Get order by id
    Given an order with id 1 exists
    When I request the order with id 1
    Then the response should be successful
    And the order total should be 100
```

```csharp
[Binding]
public class OrderSteps
{
    private Result<OrderResponse, Failure> _result;
    
    [Given("an order with id (.*) exists")]
    public void GivenAnOrderExists(int orderId)
    {
        // Setup test data
    }
    
    [When("I request the order with id (.*)")]
    public async Task WhenIRequestTheOrder(int orderId)
    {
        _result = await _service.GetOrderAsync(orderId, CancellationToken.None);
    }
    
    [Then("the response should be successful")]
    public void ThenTheResponseShouldBeSuccessful()
    {
        _result.IsSuccess.Should().BeTrue();
    }
}
```

---

## J. Common Anti-Patterns

```csharp
// ❌ 測試實作細節
await _orderRepository.Received(1).GetByIdAsync(1, Arg.Any<CancellationToken>());
// 如果實作改變（例如加入快取呼叫兩次），測試會失敗

// ✅ 測試行為
result.Value.Should().Be(expectedOrder);

// ❌ 一個測試驗證多個行為
[Fact]
public void OrderTests()
{
    // 測試 create, update, 和 delete 在同一個測試中
}

// ✅ 每個測試驗證一個行為
[Fact]
public void CreateOrder_WithValidData_ShouldCreateOrder() { }
[Fact]
public void UpdateOrder_WithValidData_ShouldUpdateOrder() { }

// ❌ 測試間共享可變狀態
private static Order _sharedOrder; // BAD!

// ✅ 每個測試都有新的 setup
public OrderServiceTests()
{
    // 為每個測試建立新的 mock
}
```
