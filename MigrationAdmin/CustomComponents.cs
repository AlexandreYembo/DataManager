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

        public bool ShowModalSaveFiles { get; set; }

        public List<DynamicData> Data { get; set; } = new();

        public void ModalOk()
        {
            ShowModal = false;
        }

        public void SaveFile()
        {
            ShowModalSaveFiles = true;
        }

        public void FileSaved()
        {
            ShowModalSaveFiles = false;
        }

        [Parameter]
        public EventCallback<DataFieldsMapping> OnDataFieldsMappingUpdated { get; set; } = new();

        [Parameter] public EventCallback<DataFieldsMapping> OnDataFieldsMappingEditing { get; set; } = new();

        [Parameter]
        public DataFieldsMapping DataFieldsMapping { get; set; }


        public async Task<DataFieldsMapping> AddDataFieldsMapping(DataFieldsMapping dataFieldsMapping, MappingType mappingType)
        {
            dataFieldsMapping.MappingType = mappingType;
            await OnDataFieldsMappingUpdated.InvokeAsync(dataFieldsMapping);

            return new DataFieldsMapping();
        }

        public async Task Edit(DataFieldsMapping dataFieldsMapping)
        {
            await OnDataFieldsMappingEditing.InvokeAsync(dataFieldsMapping);
        }
    }
}