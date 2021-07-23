using Compressarr.JobProcessing;
using Compressarr.JobProcessing.Models;
using MudBlazor;

namespace Compressarr.Application.Models
{
    public class InitialisationTask
    {
        public ConditionSwitch Condition { get; set; }
        public string Name { get; set; }
        public string State { get; set; }

        public string Icon => Condition.State switch
        {
            ConditionState.NotStarted => Icons.Rounded.HourglassTop,
            ConditionState.Processing => Icons.Rounded.PlayArrow,
            ConditionState.Succeeded => Icons.Rounded.Check,
            ConditionState.Failed => Icons.Rounded.Error,
            _ => throw new System.NotImplementedException()
        };

        public Color IconColour => Condition.State switch
        {
            ConditionState.NotStarted => Color.Default,
            ConditionState.Processing => Color.Primary,
            ConditionState.Succeeded => Color.Success,
            ConditionState.Failed => Color.Error,
            _ => throw new System.NotImplementedException()
        };

        public InitialisationTask(string name)
        {
            Name = name;
            Condition = new();
        }
    }
}
