using Microsoft.AspNetCore.Components;
using Migration.Repository.Models;
using Migration.Services.LogModels;
using Migration.Services.Models;

namespace MigrationAdmin
{
    public class CustomComponents : ComponentBase
    {
        public bool Loading = false;
        public LogResult LogResult;
        public Dictionary<string, List<Difference>> DataDifference { get; set; } = new();
        public string RecordId { get; set; }
       
        public bool ShowModal { get; set; }

        public List<DynamicData> Data { get; set; } = new();

        public void ModalOk()
        {
            ShowModal = false;
        }
    }
}