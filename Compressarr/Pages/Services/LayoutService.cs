using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Compressarr.Pages.Services
{
    public class LayoutService : ILayoutService
    {
        AuthenticationStateProvider authenticationState;

        public LayoutService(AuthenticationStateProvider authenticationStateProvider)
        {

            authenticationState = authenticationStateProvider;
        }

        private CancellationTokenSource FinishModal;
        private bool DialogResponse;

        public bool CancelRequired { get; set; }
        public bool NeedsConfirmation { get; set; }
        public bool NeedsAuthentication { get; set; }
        public bool Spinning { get; set; }

        public event EventHandler OnStateChanged;

        public MarkupString Message { get; set; }
        public MarkupString AuthenticationMessage { get; set; }
        public MarkupString ConfirmationMessage { get; set; }


        public Worker Working(string message = null) => new Worker(this, message ?? "Working");

        public void RaiseChange()
        {
            OnStateChanged?.Invoke(this, null);
        }

        public async Task<bool> ShowConfirmAsync(string message)
        {
            ConfirmationMessage = new(message);
            CancelRequired = true;
            NeedsConfirmation = true;
            RaiseChange();
            await WaitForFinish();

            return DialogResponse;
        }

        public async Task ShowDialogAsync(string message)
        {
            ConfirmationMessage = new(message);
            CancelRequired = false;
            NeedsConfirmation = true;
            RaiseChange();
            await WaitForFinish();
        }

        public async Task ShowDialogAsync(HashSet<string> messages)
        {
            if (messages.Count == 1)
            {
                await ShowDialogAsync(messages.First());
            }
            else
            {
                await ShowDialogAsync(string.Join("", messages.Select(m => $"<p>{m}</p>")));
            }
        }


        public void ConfirmDialog(bool confirmation)
        {
            DialogResponse = confirmation;
            //Only set this true if this is already true;
            NeedsAuthentication = NeedsAuthentication && confirmation;
            if (FinishModal.Token.CanBeCanceled)
            {
                FinishModal.Cancel();
            }
            NeedsConfirmation = false;
            RaiseChange();
        }

        private async Task WaitForFinish()
        {
            try
            {
                using (FinishModal = new())
                {
                    await Task.Delay(-1, FinishModal.Token);
                }
            }
            catch (TaskCanceledException) { } // we want to cancel it.
        }
    }

    /// <summary>
    /// When created this will start the spinning, when disposed it will stop it. Not really needed by external code, but need to be public for accessibility level.
    /// </summary>
    public sealed class Worker : IDisposable
    {
        private ILayoutService _parent;

        public Worker(ILayoutService parent, string message)
        {
            _parent = parent;
            _parent.Message = new MarkupString($"&nbsp;&nbsp;&nbsp;{message}&hellip;");
            _parent.Spinning = true;
            _parent.RaiseChange();
        }

        public void Dispose()
        {
            _parent.Spinning = false;
            _parent.RaiseChange();
        }
    }
}
