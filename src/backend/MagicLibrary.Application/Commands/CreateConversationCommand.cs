using ErrorOr;
using FluentValidation;
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
    private const string SystemMessage =
        "You are a library worker. Answer questions about books in the library and their contents. If a question is unrelated to books or their contents, politely decline to answer.";

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
            SystemMessage);

        return await conversationWriteRepository.CreateAsync(conversation, cancellationToken);
    }
}
