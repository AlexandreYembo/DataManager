namespace Migration.Repository.Publishers
{
    public interface IPublisher<in TEntity, TEventArgs>
                                        where TEntity : class, new()
                                        where TEventArgs : EventArgs
    {
        public event EventHandler<TEventArgs> OnEventPublished;

        public void Publish(TEntity entity);
    }
}