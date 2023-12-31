﻿using Microsoft.Azure.Cosmos;
using Migration.Repository;

namespace Migration.Infrastructure.CosmosDb
{
    public class CosmosDbConnection : ITestConnection
    {
        private readonly DataSettings _settings;
        public CosmosDbConnection(DataSettings settings)
        {
            _settings = settings;
        }
        public async Task<DataSettings> Test()
        {
            _settings.Entities = new();
            using CosmosClient client = new(_settings.GetEndpoint(), _settings.GetAuthKey());
            var database = client.GetDatabase(_settings.GetDataBase());

            // Get a reference to the container
            // var container = database.GetContainer(settings.Container);
            FeedIterator<ContainerProperties> iterator = database.GetContainerQueryIterator<ContainerProperties>();
            FeedResponse<ContainerProperties> containers = await iterator.ReadNextAsync().ConfigureAwait(false);

            foreach (var container in containers)
            {
                // do what you want with the container
                _settings.Entities.Add(container.Id);
            }

            return _settings;
        }

        public async Task<List<string>> GetContainers(DBSettings settings)
        {
            settings.ListOfContainer = new();
            using CosmosClient client = new(settings.Endpoint, settings.AuthKey);
            var database = client.GetDatabase(settings.Database);

            // Get a reference to the container
            // var container = database.GetContainer(settings.Container);
            FeedIterator<ContainerProperties> iterator = database.GetContainerQueryIterator<ContainerProperties>();
            FeedResponse<ContainerProperties> containers = await iterator.ReadNextAsync().ConfigureAwait(false);

            foreach (var container in containers)
            {
                // do what you want with the container
                settings.ListOfContainer.Add(container.Id);
            }

            if (settings.ListOfContainer.Any())
            {
                return settings.ListOfContainer;
            }

            return new List<string>();
        }
    }
}