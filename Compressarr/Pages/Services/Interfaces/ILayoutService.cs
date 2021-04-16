using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Compressarr.Pages.Services
{
    public interface ILayoutService
    {
        MarkupString AuthenticationMessage { get; set; }
        bool CancelRequired { get; set; }
        MarkupString ConfirmationMessage { get; set; }
        MarkupString Message { get; set; }
        bool NeedsAuthentication { get; set; }
        bool NeedsConfirmation { get; set; }
        bool Spinning { get; set; }

        event EventHandler OnStateChanged;

        void ConfirmDialog(bool confirmation);
        void RaiseChange();
        Task<bool> ShowConfirmAsync(string message);
        Task ShowDialogAsync(HashSet<string> messages);
        Task ShowDialogAsync(string message);
        Worker Working(string message = null);
    }
}