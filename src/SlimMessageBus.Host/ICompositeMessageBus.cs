﻿namespace SlimMessageBus.Host;

public interface ICompositeMessageBus
{
    /// <summary>
    /// Gets the child bus by name
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    IMessageBus GetChildBus(string name);

    /// <summary>
    /// Get child buses
    /// </summary>
    /// <returns></returns>
    IEnumerable<IMessageBus> GetChildBuses();
}