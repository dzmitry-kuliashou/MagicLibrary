using System.Globalization;
using System.Runtime.CompilerServices;
using ErrorOr;
using FluentValidation;
using MagicLibrary.Application.Chat;
using MagicLibrary.Application.Commands;
using MagicLibrary.Application.Queries;
using MagicLibrary.Application.Validation;
using MagicLibrary.Core.Aggregates;
using MagicLibrary.Core.ValueObjects;
using MediatR;

namespace MagicLibrary.Application.Conversations;

public sealed class LibraryConversationOrchestrator(
    IValidator<LibraryChatCommand> validator,
    ISender sender,
    ILibraryChatAgent libraryChatAgent)
    : StreamedAiConversation, ILibraryConversationOrchestrator
{
    private const int ConversationNameMaximumLength = 50;

    public async Task<ErrorOr<IAsyncEnumerable<ChatStreamEvent>>> StreamAsync(
        LibraryChatCommand command,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            return validationResult.Errors.ToErrors();
        }

        Guid conversationId;
        if (command.ConversationId is null)
        {
            var createResult = await sender.Send(
                new CreateConversationCommand(CreateConversationName(command.Message)),
                cancellationToken);
            if (createResult.IsError)
            {
                return createResult.Errors;
            }

            conversationId = createResult.Value.Id;
        }
        else
        {
            var conversationResult = await sender.Send(
                new GetConversationByIdQuery(command.ConversationId.Value),
                cancellationToken);
            if (conversationResult.IsError)
            {
                return conversationResult.Errors;
            }

            conversationId = conversationResult.Value.Id;
        }

        var userMessageResult = await sender.Send(
            new AddUserMessageCommand(conversationId, command.Message),
            cancellationToken);
        if (userMessageResult.IsError)
        {
            return userMessageResult.Errors;
        }

        var beginResponseResult = await sender.Send(
            new BeginAssistantResponseCommand(conversationId),
            cancellationToken);
        if (beginResponseResult.IsError)
        {
            return beginResponseResult.Errors;
        }

        var messages = beginResponseResult.Value.Messages;
        var history = messages.Take(messages.Count - 1).ToArray();
        var sourceReferences = new List<SourceReference>();
        return ErrorOrFactory.From(
            StreamAsync(
                conversationId,
                StreamDeltasAsync(history, command.BookId, sourceReferences, cancellationToken),
                (content, token) => CompleteAssistantResponseAsync(
                    conversationId,
                    content,
                    sourceReferences,
                    token),
                (_, token) => FailAssistantResponseAsync(conversationId, token),
                token => InterruptAssistantResponseAsync(conversationId, token),
                cancellationToken));
    }

    private Task<ErrorOr<Conversation>> CompleteAssistantResponseAsync(
        Guid conversationId,
        string content,
        IReadOnlyCollection<SourceReference> sourceReferences,
        CancellationToken cancellationToken)
        => sender.Send(
            new CompleteAssistantResponseCommand(
                conversationId,
                content,
                sourceReferences),
            cancellationToken);

    private Task<ErrorOr<Conversation>> FailAssistantResponseAsync(
        Guid conversationId,
        CancellationToken cancellationToken)
        => sender.Send(
            new FailAssistantResponseCommand(conversationId),
            cancellationToken);

    private Task<ErrorOr<Conversation>> InterruptAssistantResponseAsync(
        Guid conversationId,
        CancellationToken cancellationToken)
        => sender.Send(
            new InterruptAssistantResponseCommand(conversationId),
            cancellationToken);

    private async IAsyncEnumerable<string> StreamDeltasAsync(
        IReadOnlyList<Message> history,
        Guid? requestedBookId,
        ICollection<SourceReference> sourceReferences,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var update in libraryChatAgent
                           .StreamAsync(history, requestedBookId, cancellationToken))
        {
            if (update.SourceReferences is not null)
            {
                sourceReferences.Clear();
                foreach (var sourceReference in update.SourceReferences)
                {
                    sourceReferences.Add(sourceReference);
                }
            }

            if (!string.IsNullOrEmpty(update.Text))
            {
                yield return update.Text;
            }
        }
    }

    private static string CreateConversationName(string message)
    {
        var textInfo = new StringInfo(message);
        return textInfo.LengthInTextElements <= ConversationNameMaximumLength
            ? message
            : $"{textInfo.SubstringByTextElements(0, ConversationNameMaximumLength - 3)}...";
    }
}
