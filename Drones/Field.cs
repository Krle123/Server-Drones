namespace Library
{
    [Serializable]
    public class Field
    {
        public int humidity { get; set; } = 100;
        public int growth { get; set; } = 0;
        public FieldType Type { get; set; } = FieldType.UNCULTIVATED;
        public FieldStatus Status { get; set; } = FieldStatus.FREE;
    }
}
