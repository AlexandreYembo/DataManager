﻿using System.Reflection;

namespace Migration.Services.Publishers
{
    public class Publisher<TEntity, TEventArgs> : IPublisher<TEntity, TEventArgs>
                                                        where TEntity : class, new()
                                                        where TEventArgs : EventArgs
    {
        public event EventHandler<TEventArgs>? OnEntityChanged;

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
            if (OnEntityChanged == null) return;

            Type classType = typeof(TEventArgs);
            ConstructorInfo? classConstructor = classType.GetConstructor(new[] { entity.GetType() });

            if (classConstructor == null) return;

            TEventArgs classInstance = (TEventArgs)classConstructor.Invoke(new object[] { entity });

            OnEntityChanged(this, classInstance);
        }

        protected virtual async Task OnEventChangedAsync(TEntity entity)
        {
            if (OnEntityChanged == null) return;

            Type classType = typeof(TEventArgs);
            ConstructorInfo? classConstructor = classType.GetConstructor(new[] { entity.GetType() });

            if (classConstructor == null) return;

            TEventArgs classInstance = (TEventArgs)classConstructor.Invoke(new object[] { entity });

            await Task.Run(() => OnEntityChanged(this, classInstance));
        }
    }
}