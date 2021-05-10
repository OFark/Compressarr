using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Html;
using MudBlazor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.Services.Base
{
    public enum ServiceStatus
    {
        Checking,
        Ready,
        Partial,
        Incomplete
    }
    public class StatusResult
    {
        public StatusResult()
        {
            Status = ServiceStatus.Checking;
            Message = new("Testing");
        }

        public string Icon => Status switch { ServiceStatus.Checking => Icons.Outlined.Sync, ServiceStatus.Incomplete => Icons.Outlined.Error, ServiceStatus.Partial => Icons.Outlined.Check, ServiceStatus.Ready => Icons.Outlined.Check, _ => throw new NotImplementedException() };

        public Color Colour => Status switch { ServiceStatus.Checking => Color.Info, ServiceStatus.Incomplete => Color.Error, ServiceStatus.Partial => Color.Warning, ServiceStatus.Ready => Color.Success, _ => throw new NotImplementedException() };

        public ServiceStatus Status { get; set; }
        public MarkupString Message { get; set; }
    }
}
