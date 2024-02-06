using System.Reflection;

namespace Migration.Repository.Publishers
{
    public class Publisher<TEntity, TEventArgs> : IPublisher<TEntity, TEventArgs>
                                                        where TEntity : class, new()
                                                        where TEventArgs : EventArgs
    {
        public event EventHandler<TEventArgs>? OnEventPublished;

        public void Publish(TEntity entity)
        {
            OnEventChanged(entity);
        }

        public async Task PublishAsync(TEntity entity)
        {
            await OnEventChangedAsync(entity);
        }

        protected virtual void OnEventChanged(TEntity entity)
        {
            HasEventSubscribed();

            var classInstance = GetClassInstance(entity);

            OnEventPublished(this, classInstance);
        }

        protected virtual async Task OnEventChangedAsync(TEntity entity)
        {
            HasEventSubscribed();

            var classInstance = GetClassInstance(entity);

            await Task.Run(() => OnEventPublished(this, classInstance));
        }

        private void HasEventSubscribed()
        {
            if (OnEventPublished == null)
            {
                throw new ArgumentException("No event subscribed for this Publisher, you must need to subscribe at least one event in order to publish it");
            }
        }

        private static TEventArgs GetClassInstance(TEntity entity)
        {
            Type classType = typeof(TEventArgs);
            ConstructorInfo? classConstructor = classType.GetConstructor(new[] { entity.GetType() });

            if (classConstructor == null)
            {
                throw new ArgumentException("You must need to provide your EventArgs class with a constructor with one parameter. Example: public ActionsEventArgs(Actions actions)");
            }

            TEventArgs classInstance = (TEventArgs)classConstructor.Invoke(new object[] { entity });

            return classInstance;
        }
    }
}