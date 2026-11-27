using ErrorOr;
using FluentValidation;
using MagicLibrary.Application.Chat;
using MagicLibrary.Application.Validation;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.Repositories.Documents;
using MediatR;

namespace MagicLibrary.Application.Commands;

public sealed record CreateConversationCommand(string Name) : IRequest<ErrorOr<Conversation>>;

public sealed class CreateConversationCommandHandler(
    IValidator<CreateConversationCommand> validator,
    TimeProvider timeProvider,
    IConversationWriteRepository conversationWriteRepository)
    : IRequestHandler<CreateConversationCommand, ErrorOr<Conversation>>
{
    public async Task<ErrorOr<Conversation>> Handle(
        CreateConversationCommand request,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return validationResult.Errors.ToErrors();
        }

        var conversation = Conversation.Create(
            request.Name,
            timeProvider.GetUtcNow(),
            LibraryChatInstructions.SystemMessage);

        return await conversationWriteRepository.CreateAsync(conversation, cancellationToken);
    }
}
