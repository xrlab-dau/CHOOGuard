namespace ChooGuard.App.Fps.Equipment
{
    /// <summary>
    /// A component on the root of an equipment prefab that needs the placement entry's data (<see cref="StationEquipment.Data"/>) before its renderers are
    /// collected for batching: the spawner calls <see cref="OnPlaced"/> right after <see cref="StationEquipment.Assign"/> (a pipe builds its mesh from its
    /// end points here). Anything that only reads the data later does not need it. Only components on the prefab root are asked: a query of the whole hierarchy
    /// costs too much for thousands of fittings.
    /// </summary>
    public interface IEquipmentPlaced
    {
        void OnPlaced();
    }
}
