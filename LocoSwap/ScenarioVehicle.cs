using System;

namespace LocoSwap
{
    public class ScenarioVehicle : Vehicle
    {
        private int _idx;
        private string _number;
        private bool _flipped;
        private string _possibleSubstitutionDisplayName;

        public int Idx
        {
            get => _idx;
            set => SetProperty(ref _idx, value);
        }
        public string Number
        {
            get => _number;
            set => SetProperty(ref _number, value);
        }
        public bool Flipped
        {
            get => _flipped;
            set => SetProperty(ref _flipped, value);
        }
        public string PossibleSubstitutionDisplayName
        {
            get => _possibleSubstitutionDisplayName;
            set => SetProperty(ref _possibleSubstitutionDisplayName, value);
        }
        public bool IsInvolvedInConsistOperation { get; set; }

        public ScenarioVehicle() : base()
        {
            Idx = -1;
            Number = "";
            Flipped = false;
        }

        public ScenarioVehicle(int idx, string provider, string product, string blueprintId, string name, string number, bool flipped, float length, bool isInvolvedInConsistOperation)
            : base(provider, product, blueprintId, name, length)
        {
            Idx = idx;
            Number = number;
            Flipped = flipped;
            IsInvolvedInConsistOperation = isInvolvedInConsistOperation;
        }
        public void CopyFrom(AvailableVehicle from)
        {
            Provider = from.Provider;
            Product = from.Product;
            BlueprintId = from.BlueprintId;
            Name = from.Name;
            Exists = VehicleExistance.Replaced;
            Type = from.Type;
            DisplayName = from.DisplayName;

            if (from.IsReskin)
            {
                IsReskin = true;
                ReskinBlueprintId = from.ReskinBlueprintId;
                ReskinProvider = from.ReskinProvider;
                ReskinProduct = from.ReskinProduct;
            }
            else
            {
                IsReskin = false;
                ReskinBlueprintId = "";
                ReskinProvider = "";
                ReskinProduct = "";
            }

            if (!IsInvolvedInConsistOperation && from.NumberingList.Count > 0)
            {
                int index = Utilities.StaticRandom.Instance.Next(from.NumberingList.Count);
                Number = from.NumberingList[index];
            }
        }
    }
}
