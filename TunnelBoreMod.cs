namespace TunnelBore
{
    public class TunnelBoreMod : FortressCraftMod
    {
        public ushort TunnelBoreCubeType { get; set; }

        public override ModRegistrationData Register()
        {
            var registrationData = new ModRegistrationData();
            registrationData.RegisterEntityHandler("Innominate.BoringCompany");
            TerrainData.GetCubeByKey("Innominate.BoringCompany", out var entry, out var valueEntry);
            if (entry != null)
                this.TunnelBoreCubeType = entry.CubeType;
            return registrationData;
        }

        public override ModCreateSegmentEntityResults CreateSegmentEntity(ModCreateSegmentEntityParameters parameters)
        {
            var results = new ModCreateSegmentEntityResults();
            if (parameters.Cube == this.TunnelBoreCubeType)
            {
                results.Entity = new TunnelBore(parameters.X, parameters.Y, parameters.Z, parameters.Cube,
                    parameters.Flags, parameters.Value, parameters.Position, parameters.Segment);
            }

            return results;
        }
    }
}