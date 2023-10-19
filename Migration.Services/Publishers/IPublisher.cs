namespace Migration.Services.Publishers
{
    public interface IPublisher<in TEntity, TEventArgs>
                                        where TEntity : class, new()
                                        where TEventArgs : EventArgs
    {
        public event EventHandler<TEventArgs> OnEntityChanged;

        public void Publish(TEntity entity);
    }
}