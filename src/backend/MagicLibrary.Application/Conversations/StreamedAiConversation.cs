using System.Runtime.CompilerServices;
using System.Text;
using ErrorOr;
using MagicLibrary.Application.Chat;
using MagicLibrary.Core.Aggregates;

namespace MagicLibrary.Application.Conversations;

public abstract class StreamedAiConversation
{
    protected async IAsyncEnumerable<ChatStreamEvent> StreamAsync(
        Guid conversationId,
        IAsyncEnumerable<string> deltas,
        Func<string, CancellationToken, Task<ErrorOr<Conversation>>> onSuccess,
        Func<Exception, CancellationToken, Task<ErrorOr<Conversation>>> onFailed,
        Func<CancellationToken, Task<ErrorOr<Conversation>>> onInterrupted,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var responseContent = new StringBuilder();
        var aiAgentTerminalStatusPersisted = false;
        Exception? generationError = null;
        var generationWasCancelled = false;

        try
        {
            yield return new ChatStreamEvent.Status(conversationId);

            IAsyncEnumerator<string>? enumerator = null;
            try
            {
                enumerator = deltas.GetAsyncEnumerator(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                generationWasCancelled = true;
            }
            catch (Exception exception)
            {
                generationError = exception;
            }

            if (enumerator is not null)
            {
                try
                {
                    while (true)
                    {
                        bool hasNext;
                        string delta;
                        try
                        {
                            hasNext = await enumerator.MoveNextAsync();
                            delta = hasNext ? enumerator.Current : string.Empty;
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                            generationWasCancelled = true;
                            break;
                        }
                        catch (Exception exception)
                        {
                            generationError = exception;
                            break;
                        }

                        if (!hasNext)
                        {
                            break;
                        }

                        responseContent.Append(delta);
                        yield return new ChatStreamEvent.Text(delta);
                    }
                }
                finally
                {
                    try
                    {
                        await enumerator.DisposeAsync();
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        generationWasCancelled = true;
                    }
                    catch (Exception exception)
                    {
                        generationError ??= exception;
                    }
                }
            }

            if (generationWasCancelled)
            {
                yield break;
            }

            if (generationError is not null)
            {
                var failureResult = await onFailed(generationError, CancellationToken.None);
                aiAgentTerminalStatusPersisted = !failureResult.IsError;
                var errorMessage = generationError.Message;
                if (failureResult.IsError)
                {
                    errorMessage = $"{errorMessage}{Environment.NewLine}{FormatErrors(failureResult.Errors)}";
                }

                yield return new ChatStreamEvent.Error(errorMessage);
                yield break;
            }

            ErrorOr<Conversation> successResult;
            Exception? completionError = null;
            try
            {
                successResult = await onSuccess(responseContent.ToString(), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                generationWasCancelled = true;
                successResult = default;
            }
            catch (Exception exception)
            {
                completionError = exception;
                successResult = default;
            }

            if (generationWasCancelled)
            {
                yield break;
            }

            if (completionError is not null || successResult.IsError)
            {
                var errorMessage = completionError?.Message ?? FormatErrors(successResult.Errors);
                var failureResult = await onFailed(
                    completionError ?? new InvalidOperationException(errorMessage),
                    CancellationToken.None);
                aiAgentTerminalStatusPersisted = !failureResult.IsError;
                if (failureResult.IsError)
                {
                    errorMessage = $"{errorMessage}{Environment.NewLine}{FormatErrors(failureResult.Errors)}";
                }

                yield return new ChatStreamEvent.Error(errorMessage);
                yield break;
            }

            aiAgentTerminalStatusPersisted = true;
            yield return new ChatStreamEvent.Completed();
        }
        finally
        {
            if (!aiAgentTerminalStatusPersisted && cancellationToken.IsCancellationRequested)
            {
                var interruptionResult = await onInterrupted(CancellationToken.None);
                if (interruptionResult.IsError)
                {
                    throw new InvalidOperationException(FormatErrors(interruptionResult.Errors));
                }
            }
        }
    }

    private static string FormatErrors(IEnumerable<Error> errors) =>
        string.Join(Environment.NewLine, errors.Select(error => error.Description));
}
