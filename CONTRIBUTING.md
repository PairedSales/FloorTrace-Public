# Contributing to FloorTrace

Thank you for your interest in contributing to FloorTrace! This document provides guidelines and instructions for contributing to the project.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [How Can I Contribute?](#how-can-i-contribute)
- [Development Setup](#development-setup)
- [Coding Standards](#coding-standards)
- [Submitting Changes](#submitting-changes)
- [Reporting Bugs](#reporting-bugs)
- [Suggesting Features](#suggesting-features)

## Code of Conduct

This project adheres to a code of conduct that all contributors are expected to follow. Please be respectful and constructive in all interactions.

### Our Standards

- Use welcoming and inclusive language
- Be respectful of differing viewpoints and experiences
- Accept constructive criticism gracefully
- Focus on what is best for the community
- Show empathy towards other community members

## How Can I Contribute?

### Reporting Bugs

Before creating a bug report, please check the existing issues to avoid duplicates.

**When submitting a bug report, include:**
- Clear and descriptive title
- Detailed steps to reproduce the problem
- Expected vs. actual behavior
- Screenshots or screen recordings if applicable
- Your system information:
  - Windows version
  - .NET version
  - FloorTrace version
- Log files from `%LOCALAPPDATA%/FloorTrace/Logs/`

### Suggesting Features

Feature requests are welcome! Please provide:
- Clear and descriptive title
- Detailed description of the proposed feature
- Explanation of why this feature would be useful
- Examples of how the feature would work
- Any relevant mockups or diagrams

### Code Contributions

1. **Fork the repository**
2. **Create a feature branch** from `main`:
   ```bash
   git checkout -b feature/your-feature-name
   ```
3. **Make your changes**
4. **Test thoroughly**
5. **Commit with clear messages**
6. **Push to your fork**
7. **Submit a pull request**

## Development Setup

### Prerequisites

- **Visual Studio 2022** or later (Community Edition is fine)
- **.NET 8.0 SDK**
- **Git**

### Building from Source

```bash
# Clone your fork
git clone https://github.com/YOUR-USERNAME/FloorTrace-Public.git
cd FloorTrace-Public

# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run the application
dotnet run
```

### Running Tests

```bash
# Run all tests
dotnet test FloorTrace.Tests/

# Run with coverage
dotnet test FloorTrace.Tests/ /p:CollectCoverage=true
```

## Coding Standards

### C# Style Guide

Follow these conventions:

- **Naming**:
  - PascalCase for classes, methods, properties, events
  - camelCase for local variables and parameters
  - _camelCase for private fields (with underscore prefix)
  - UPPER_CASE for constants

- **Code Organization**:
  - One class per file
  - File name matches class name
  - Organize members: fields, constructors, properties, methods
  - Group interface implementations together

- **Documentation**:
  - XML comments for all public APIs
  - Inline comments for complex logic
  - Clear commit messages

- **Error Handling**:
  - Use try-catch appropriately
  - Provide meaningful error messages
  - Log errors with context
  - Never swallow exceptions silently

- **Async/Await**:
  - Use `async`/`await` for I/O operations
  - All async methods end with `Async` suffix
  - Use `ConfigureAwait(false)` in library code
  - Avoid `async void` (use `async Task` instead)

### XAML Style Guide

- Use proper indentation (2 spaces)
- Place one attribute per line for complex controls
- Use data binding where appropriate
- Follow MVVM pattern

### Example Code

```csharp
/// <summary>
/// Calculates the area of a polygon using Green's theorem.
/// </summary>
/// <param name="points">The vertices of the polygon.</param>
/// <returns>The area in square units.</returns>
public async Task<double> CalculateAreaAsync(List<PointF> points)
{
    if (points == null || points.Count < 3)
    {
        throw new ArgumentException("At least 3 points are required.", nameof(points));
    }

    try
    {
        _logger.LogInformation("Calculating area for polygon with {Count} points", points.Count);
        
        // Implementation here
        var area = await ComputeAreaAsync(points).ConfigureAwait(false);
        
        return area;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to calculate area");
        throw;
    }
}
```

## Submitting Changes

### Pull Request Process

1. **Update documentation** if you changed APIs or added features
2. **Add tests** for new functionality
3. **Ensure all tests pass**
4. **Update CHANGES.md** with a brief description of your changes
5. **Follow the PR template** (will be added if accepted)
6. **Link related issues** using keywords (Fixes #123, Closes #456)

### Commit Message Guidelines

Use clear, descriptive commit messages:

```
Short summary (50 chars or less)

More detailed explanation if needed. Wrap at 72 characters.
Explain the problem this commit solves and why you chose
this particular solution.

- Bullet points are okay
- Use present tense ("Add feature" not "Added feature")
- Reference issues: Fixes #123
```

### Code Review Process

- All submissions require review
- Reviewers may request changes
- Be responsive to feedback
- Once approved, maintainers will merge

## Project Structure

Understanding the codebase:

```
FloorTrace/
├── Models/              # Data models
├── ViewModels/          # MVVM ViewModels
├── Services/            # Service layer (DI)
├── Controls/            # Custom WPF controls
├── Utilities/           # Helper classes
└── FloorTrace.Tests/    # Unit tests
```

### Key Technologies

- **.NET 8.0** - Runtime
- **WPF** - UI framework
- **CommunityToolkit.Mvvm** - MVVM helpers
- **OpenCvSharp4** - Computer vision
- **SkiaSharp** - Graphics
- **Serilog** - Logging
- **xUnit** - Testing

## Testing Guidelines

### Unit Tests

- Write tests for all new features
- Follow Arrange-Act-Assert pattern
- Use descriptive test names
- Mock external dependencies
- Aim for >80% code coverage

### Example Test

```csharp
[Fact]
public async Task CalculateAreaAsync_WithValidPolygon_ReturnsCorrectArea()
{
    // Arrange
    var service = new AreaCalculationService();
    var points = new List<PointF>
    {
        new PointF(0, 0),
        new PointF(10, 0),
        new PointF(10, 10),
        new PointF(0, 10)
    };
    var expected = 100.0;

    // Act
    var result = await service.CalculateAreaAsync(points);

    // Assert
    Assert.Equal(expected, result, precision: 2);
}
```

## Questions?

If you have questions about contributing:

1. Check existing [Issues](https://github.com/PairedSales/FloorTrace-Public/issues)
2. Review [Documentation](README.md)
3. Open a new [Discussion](https://github.com/PairedSales/FloorTrace-Public/discussions)

## License

By contributing, you agree that your contributions will be licensed under the MIT License.

---

Thank you for contributing to FloorTrace! 🎉

