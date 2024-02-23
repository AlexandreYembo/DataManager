### Configurations applied when you are creating and / or importing a new data

###### If you are importing data to Cosmos Db, you should use the convention such as:

| Key      | Value | Required|
| ----------- | ----------- | --------- |
| PartitionKey     | example: id      |yes|
| RecordId     | example: id      |yes|

1. If you do not provide this information, and your table already have a parition key defined, you will get an error during the process.

2. If you do not provide this information and you are about to create a new table, you will not be able to do so. You must need to provide this parameter beforehand.

3. You must need to provide the format in of the key as it shows in the table, it is not case sensitive

4. You must need to provide in the value exactly the value configured in the target table if already exists.

5. Partition Key also has to be defined in your mapping between Source and Target. If you add a wrong value there will have errors during migration.


##### When you are using Table Storage you must need to add the extra definition for RowKey:

| Key      | Value | Required|
| ----------- | ----------- | --------- |
| PartitionKey     | example: PartitionKey      |yes|
| RecordId     | example: PartitionKey      |yes|
| RowKey     | example: PartitionKey      |yes|