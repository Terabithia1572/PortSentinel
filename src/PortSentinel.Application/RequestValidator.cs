using FluentValidation;
using PortSentinel.Contracts;

namespace PortSentinel.Application;

public sealed class RequestValidator : AbstractValidator<Request>
{
    public RequestValidator()
    {
        RuleFor(r => r.Version).Equal(PipeProtocol.Version);
        RuleFor(r => r.Operation).IsInEnum();
        RuleFor(r => r.DeviceId).NotEmpty().MaximumLength(512).When(r => r.Operation is Operation.Grant or Operation.Revoke or Operation.Rename);
        RuleFor(r => r.Name).NotEmpty().MaximumLength(80).Must(n => n is not null && !n.Any(char.IsControl))
            .When(r => r.Operation is Operation.Grant or Operation.Rename);
        RuleFor(r => r.ExpectedRevision).GreaterThanOrEqualTo(0);
        RuleFor(r => r.ScanIntervalSeconds).InclusiveBetween(3, 60).When(r => r.Operation == Operation.Settings);
        RuleFor(r => r.RetentionDays).InclusiveBetween(1, 90).When(r => r.Operation == Operation.Settings);
    }
}
