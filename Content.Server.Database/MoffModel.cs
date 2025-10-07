using System;

namespace Content.Server.Database;

public static class MoffModel
{
    public class MoffAntagWeights
    {
        public Guid UserId { get; set; }

        public int Weight { get; set; }
    }
}
