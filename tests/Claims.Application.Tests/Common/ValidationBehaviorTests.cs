using Claims.Application.Common.Behaviors;
using FluentAssertions;
using FluentValidation;
using MediatR;

namespace Claims.Application.Tests.Common;

public sealed class ValidationBehaviorTests
{
    private sealed record SampleRequest(string Name) : IRequest<string>;

    private sealed class SampleValidator : AbstractValidator<SampleRequest>
    {
        public SampleValidator() => RuleFor(r => r.Name).NotEmpty();
    }

    [Fact]
    public async Task Handle_InvalidRequest_ThrowsValidationExceptionWithoutCallingNext()
    {
        var behavior = new ValidationBehavior<SampleRequest, string>([new SampleValidator()]);
        var nextCalled = false;

        var act = () => behavior.Handle(new SampleRequest(""), _ => { nextCalled = true; return Task.FromResult("ok"); }, CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationException>())
            .Which.Errors.Should().ContainSingle(e => e.PropertyName == nameof(SampleRequest.Name));
        nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsHandlerResult()
    {
        var behavior = new ValidationBehavior<SampleRequest, string>([new SampleValidator()]);

        var result = await behavior.Handle(new SampleRequest("valid"), _ => Task.FromResult("ok"), CancellationToken.None);

        result.Should().Be("ok");
    }
}
