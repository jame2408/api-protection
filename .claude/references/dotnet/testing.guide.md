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
        .Returns(expectedOrder);
    
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
        .Returns((Order?)null);
    
    // Act
    var result = await _service.GetOrderAsync(invalidId, CancellationToken.None);
    
    // Assert
    result.IsFailure.Should().BeTrue();
    result.Error.Code.Should().Be(GetOrderFailureCodes.OrderNotFound);
}
```

---

## D. Test Organization

### 專案結構

```
backend/tests/
├── FunctionalTests/                     # Reqnroll BDD functional tests
│   ├── ApiKeyManagement.FunctionalTests.csproj
│   └── Features/
│       └── {BoundedContext}/*.feature
├── Architecture.Tests/                  # NetArchTest architecture rules
│   └── ApiKeyManagement.Architecture.Tests.csproj
└── TestInfrastructure/                  # Shared test helpers, fixtures, builders
    └── ApiKeyManagement.TestInfrastructure.csproj
```

> Handler unit tests are not yet split into a dedicated project. When/if a
> `backend/tests/UnitTests/` project is introduced, Handler-level unit tests
> belong there; until then, do not write unit tests against a path that does
> not exist.

### Test Class 組織

Service 與 Handler 不持有 `ILogger`（CLAUDE.md 規則），因此測試也不需要 mock logger：

```csharp
public class CreateApiKeyHandlerTests
{
    private readonly IConsumerValidator _consumerValidator = Substitute.For<IConsumerValidator>();
    private readonly IApiKeyRepository _keyRepository = Substitute.For<IApiKeyRepository>();
    private readonly IScopeRegistry _scopeRegistry = Substitute.For<IScopeRegistry>();
    private readonly IAccessPolicyService _accessPolicyService = Substitute.For<IAccessPolicyService>();
    private readonly CreateApiKeyHandler _handler;

    public CreateApiKeyHandlerTests()
    {
        _handler = new CreateApiKeyHandler(
            _consumerValidator,
            _keyRepository,
            _scopeRegistry,
            _accessPolicyService);
    }

    [Fact]
    public async Task HandleAsync_WhenAllGuardsPass_ShouldReturnCreatedKey()
    {
        // ...
    }

    [Fact]
    public async Task HandleAsync_WhenActiveKeyLimitExceeded_ShouldReturnFailure()
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

// 回傳 Result（Handler / Service dependency 使用 implicit conversion）
_orderService.GetOrderAsync(1, Arg.Any<CancellationToken>())
    .Returns(new OrderResponse { Id = 1 });

// 回傳 Failure（引用 per-BC 常數）
_orderService.GetOrderAsync(-1, Arg.Any<CancellationToken>())
    .Returns(FailureProvider.CreateFailure(GetOrderFailureCodes.OrderNotFound));

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
result.Error.Code.Should().Be(GetOrderFailureCodes.OrderNotFound);

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
| 時間相關操作（`IClock`/`TimeProvider`） | Pure functions |
| 檔案系統操作 | 靜態工具方法 |
| 跨 BC 介面（`SharedKernel.Contracts.I*`） | Result/Failure 等 SharedKernel primitives |

> ⚠️ Service 與 Handler 不持有 `ILogger`，因此測試端**不需要**也**不應該**
> mock `ILogger<T>`。Boundary 層（Endpoint、Middleware、Pipeline Behavior）的
> logging 行為通常透過整合/功能測試驗證，而非單元測試。

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
