﻿namespace Migration.Models
{
    public class Difference
    {
        public string PropertyName { get; set; }
        public string Object1Value { get; set; }
        public string Object2Value { get; set; }
        public OperationType OperationType { get; set; }
    }
}