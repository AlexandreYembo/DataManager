## Using queries in conditions for mappings

1. if you are using conditions you can use queries to match values in order to perform an update in case that the condition is true.

2. Only during the conditions, you can define if you want to check the condition based on data from Source table or for Target.

3.  Conditions can be aggregated by the operation ```and``` or ```or```


#### Type of conditions supported:

##### 1- Applying conditions in properties.

In this scenario you might want to check if a property has a value, and if this is the case you can use the follow sintax:

| Syntax      | Type |
| ----------- | ----------- |
| Field1 == "value1"      | string       |
| Field1 == true   | boolean        | 
| Field1 == 2   | integer        |

##### 2- Applying conditions in arrays.

Applying conditions in array requires to use operators that can scan the object in order to fetch data that is valid.

| Syntax      | Type |
| ----------- | ----------- |
| YourArray.Any(Field1 == 'value1')     | string       |
| YourArray.Any(Field1 == true)    | boolean        | 
| YourArray.Any(Field1 == 2)   | integer        |

Currently the only operators supported for Arrays are ```Any``` and ```Contains```

###### *** limitations of using Contains operator, currently can be used only in arrays of string, but not in arrays of object. For this one you can use the Any operator.

| Syntax      | Type |
| ----------- | ----------- |
| YourArray.Contains('value1')     | string       |


#### Multiple conditions

There is support for multiple conditions in which you can choose either ```and```or ```or```. You can make combination as many as you want and for any kind of properties.

Data: 
``` json
    {
        "Car": "Audi",
        "Color":"Red",
        "IsNew": true
    }
```

|Condition | Syntax      |
| ----------- | ----------- |
|Condition 1| Car == "Audi"  and  IsNew == true|
|Condition 2| Car == "Audi"  or  Car == "Ford"|

In both scenario above either conditions will met. Despite of second Condition the car is not Ford, as was applied ```or``` it will consider the condition valid.

 Condition | Syntax          |Operator|
| ----------- | ----------- |
|Condition 1| Car == "Audi" | And|
|Condition 2| IsNew == true" | And|
|Condition 3| Car == "Audi" | or|
|Condition 4|  Car == "Ford" ||

the example above has the same behaviour, the Difference that instead of creating 2 conditions per line, we have split in 4 distinct conditions. The result will be the same as the first example