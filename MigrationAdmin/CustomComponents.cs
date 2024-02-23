using Microsoft.AspNetCore.Components;
using Migration.Models;
using Migration.Models.Logs;
using Migration.Models.Profile;
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

        public bool ShowModalSaveData { get; set; }

        public List<DynamicData> Data { get; set; } = new();

        public void ModalOk()
        {
            ShowModal = false;
        }

        public void SaveFile()
        {
            ShowModalSaveData = true;
        }

        public void FileSaved()
        {
            ShowModalSaveData = false;
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

        public void AddCondition(List<SearchCondition> searchConditions)
        {
            if (searchConditions.Any())
            {
                searchConditions.Add(new SearchCondition()
                {
                    Type = SearchConditionType.And
                });
            }
            else
            {
                searchConditions.Add(new SearchCondition());
            }
        }

        public void RemoveCondition(List<SearchCondition> searchConditions, SearchCondition searchCondition)
        {
            searchConditions.Remove(searchCondition);
        }

        public List<CustomAttributes> UpdateCustomAttribute(List<CustomAttributes> existingCustomAttributes,
            CustomAttributes newCustomAttribute)
        {
            existingCustomAttributes.RemoveAll(r => r.Key == newCustomAttribute.Key);

            existingCustomAttributes.Add(newCustomAttribute);

            return existingCustomAttributes;
        }
    }
}