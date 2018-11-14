namespace RestServer.Util
{
    internal class ConsumableData<T>
    {
        public T Data { get; set; }
        public bool IsConsumed { get; set; }
    }
}