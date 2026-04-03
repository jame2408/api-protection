# General Programming Principles

Universal software engineering best practices applicable to all languages and frameworks.

---

## SOLID Principles

### S - Single Responsibility Principle (SRP)

> A class should have only one reason to change.

**Good Example:**
```csharp
// Each class has one responsibility
public class OrderValidator { /* validation logic */ }
public class OrderRepository { /* data access */ }
public class OrderNotifier { /* send notifications */ }
```

**Bad Example:**
```csharp
// One class doing everything
public class OrderService {
    public void Validate() { /* ... */ }
    public void SaveToDatabase() { /* ... */ }
    public void SendEmail() { /* ... */ }
    public void GenerateReport() { /* ... */ }
}
```

**Detection:** Ask "What does this class do?" If the answer contains "and", it likely violates SRP.

---

### O - Open/Closed Principle (OCP)

> Software entities should be open for extension but closed for modification.

**Good Example:**
```csharp
public interface IDiscountStrategy {
    decimal Calculate(Order order);
}

public class PercentageDiscount : IDiscountStrategy { /* ... */ }
public class FixedAmountDiscount : IDiscountStrategy { /* ... */ }
// New discount types can be added without modifying existing code
```

**Bad Example:**
```csharp
public decimal CalculateDiscount(Order order, string discountType) {
    switch (discountType) {
        case "percentage": return /* ... */;
        case "fixed": return /* ... */;
        // Must modify this method for every new discount type
    }
}
```

---

### L - Liskov Substitution Principle (LSP)

> Objects of a superclass should be replaceable with objects of its subclasses without breaking the application.

**Good Example:**
```csharp
public abstract class Bird {
    public abstract void Move();
}

public class Sparrow : Bird {
    public override void Move() => Fly();
}

public class Penguin : Bird {
    public override void Move() => Walk(); // Penguins can't fly, but they can move
}
```

**Bad Example:**
```csharp
public class Bird {
    public virtual void Fly() { /* ... */ }
}

public class Penguin : Bird {
    public override void Fly() {
        throw new NotSupportedException(); // Violates LSP
    }
}
```

---

### I - Interface Segregation Principle (ISP)

> Clients should not be forced to depend on interfaces they do not use.

**Good Example:**
```csharp
public interface IReadable { T Read<T>(string id); }
public interface IWritable { void Write<T>(T entity); }
public interface IDeletable { void Delete(string id); }

// Cache only needs read
public class CacheService : IReadable { /* ... */ }
```

**Bad Example:**
```csharp
public interface IRepository {
    T Read<T>(string id);
    void Write<T>(T entity);
    void Delete(string id);
    void BulkInsert<T>(IEnumerable<T> entities);
    void ExecuteStoredProcedure(string name);
    // Clients must implement all methods even if they don't use them
}
```

---

### D - Dependency Inversion Principle (DIP)

> High-level modules should not depend on low-level modules. Both should depend on abstractions.

**Good Example:**
```csharp
public class OrderService {
    private readonly IOrderRepository _repository;
    private readonly INotificationService _notifier;
    
    public OrderService(IOrderRepository repository, INotificationService notifier) {
        _repository = repository;
        _notifier = notifier;
    }
}
```

**Bad Example:**
```csharp
public class OrderService {
    private readonly SqlOrderRepository _repository = new SqlOrderRepository();
    private readonly EmailNotifier _notifier = new EmailNotifier();
    // Tightly coupled to concrete implementations
}
```

---

## Other Key Principles

### DRY - Don't Repeat Yourself

> Every piece of knowledge must have a single, unambiguous representation in the system.

**Signs of violation:**
- Copy-pasting code blocks
- Same validation logic in multiple places
- Constants defined in multiple files

**Refactoring strategies:**
- Extract method/class
- Create shared utilities
- Use inheritance or composition

---

### KISS - Keep It Simple, Stupid

> Simplicity should be a key goal in design.

**Signs of over-engineering:**
- Abstractions with only one implementation
- Design patterns used "just in case"
- Premature optimization
- Complex configuration for simple tasks

**Questions to ask:**
- Can a junior developer understand this in 5 minutes?
- Does this solve a current problem or a hypothetical one?

---

### YAGNI - You Aren't Gonna Need It

> Don't add functionality until it's actually needed.

**Common violations:**
- Adding "just in case" parameters
- Creating interfaces for single implementations
- Building extensibility points never used

---

### Fail Fast

> Detect and report errors as early as possible.

**Implementation:**
```csharp
public void ProcessOrder(Order order) {
    ArgumentNullException.ThrowIfNull(order);
    if (order.Items.Count == 0) 
        throw new ArgumentException("Order must have items");
    
    ProcessValidOrder(order);
}
```

---

### Composition Over Inheritance

> Favor object composition over class inheritance.

**When to use inheritance:**
- IS-A relationship (Dog IS-A Animal)
- Shared behavior across all subclasses
- Framework requirements (e.g., Controller base class)

**When to use composition:**
- HAS-A relationship (Car HAS-A Engine)
- Behavior varies independently
- Need flexibility to swap implementations

---

## Code Smells

Quick reference for common code quality issues:

| Smell | Description | Refactoring |
|-------|-------------|-------------|
| **Long Method** | Method > 20-30 lines | Extract Method |
| **Large Class** | Class > 200-300 lines | Extract Class |
| **Long Parameter List** | > 3-4 parameters | Introduce Parameter Object |
| **Duplicate Code** | Same code in multiple places | Extract Method/Class |
| **Feature Envy** | Method uses another class more than its own | Move Method |
| **Data Clumps** | Same group of data appearing together | Extract Class |
| **Primitive Obsession** | Using primitives instead of small objects | Value Objects |
| **Switch Statements** | Complex switch/if-else chains | Strategy Pattern |
| **Speculative Generality** | Unused abstractions | Remove, YAGNI |
| **Dead Code** | Unused code | Delete it |
