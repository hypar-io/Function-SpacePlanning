namespace SpacePlanning
{
    public static class MessageManager
    {
        private static SpacePlanningOutputs _outputs;
        public static void Initialize(SpacePlanningOutputs outputs)
        {
            _outputs = outputs;
        }

        public static void AddWarning(string warning)
        {
            _outputs.Warnings.Add(warning);
        }

        public static void AddError(string error)
        {
            _outputs.Errors.Add(error);
        }
    }
}