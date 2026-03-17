namespace Inventory
{
    public enum PlacementMode
    {
        Oriented,  // Uses existing X/Y/Z orientation (panels, ramps)
        FullCell   // Occupies entire cell, no orientation (blocks, pillars)
    }
}
