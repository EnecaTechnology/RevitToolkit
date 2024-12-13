﻿using System.ComponentModel;
using Autodesk.Revit.UI;

namespace Eneca.Revit.Toolkit.External.Handlers;

/// <summary>
///     Handler, to provide access to modify the Revit document.
/// </summary>
/// <remarks>Suitable for cases where it is needed to await the completion of an external event.</remarks>
[PublicAPI]
public sealed class AsyncEventHandler : ExternalEventHandler
{
    private Action<UIApplication>? _action;
    
#if NETCOREAPP
    private TaskCompletionSource? _resultTask;
#else
    private TaskCompletionSource<bool>? _resultTask;
#endif

    /// <summary>Callback invoked by Revit. Not used to be called in user code.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override void Execute(UIApplication uiApplication)
    {
        try
        {
            _action!.Invoke(uiApplication);
            if (_resultTask is null) return; //Revit In Api mode
            
#if NETCOREAPP
            _resultTask.SetResult();
#else
            _resultTask.SetResult(false);
#endif
        }
        catch (Exception exception)
        {
            if (_resultTask is null) throw; //Revit In Api mode
            _resultTask.SetException(exception);
        }
        finally
        {
            _action = null;
            _resultTask = null;
        }
    }

    /// <summary>
    ///     Instructing Revit to queue a handler, raise (signal) the external event and async awaiting for its completion.
    /// </summary>
    /// <remarks>
    ///     This method async awaiting completion of the <see cref="AsyncEventHandler.Execute" /> method. <br />
    ///     Exceptions in the delegate will not be ignored and will be rethrown in the original synchronization context.<br />
    ///     <see cref="System.Threading.Tasks.Task.WaitAll(System.Threading.Tasks.Task[])" />,
    ///     <see cref="System.Threading.Tasks.Task.Wait()" /> will cause a deadlock.<br/><br/>
    ///     Executes the handler out of queue if Revit is in API mode.
    /// </remarks>
    public async Task RaiseAsync(Action<UIApplication> action)
    {
        if (_action is null) _action = action;
        else _action += action;

        if (Context.IsRevitInApiMode)
        {
            Execute(Context.UiApplication);
            return;
        }
        
#if NETCOREAPP
        _resultTask ??= new TaskCompletionSource();
#else
        _resultTask ??= new TaskCompletionSource<bool>();
#endif
        
        Raise();
        await _resultTask.Task;
    }
}