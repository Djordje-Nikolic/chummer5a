// <auto-generated>
// Code generated by Microsoft (R) AutoRest Code Generator.
// Changes may cause incorrect behavior and will be lost if the code is
// regenerated.
// </auto-generated>

namespace SINners.Models
{
    using Newtonsoft.Json;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    public partial class ResultSinnerGetOwnedSINByAlias
    {
        /// <summary>
        /// Initializes a new instance of the ResultSinnerGetOwnedSINByAlias
        /// class.
        /// </summary>
        public ResultSinnerGetOwnedSINByAlias()
        {
            CustomInit();
        }

        /// <summary>
        /// Initializes a new instance of the ResultSinnerGetOwnedSINByAlias
        /// class.
        /// </summary>
        public ResultSinnerGetOwnedSINByAlias(IList<SINner> mySINners = default(IList<SINner>), object myException = default(object), bool? callSuccess = default(bool?), string errorText = default(string))
        {
            MySINners = mySINners;
            MyException = myException;
            CallSuccess = callSuccess;
            ErrorText = errorText;
            CustomInit();
        }

        /// <summary>
        /// An initialization method that performs custom operations like setting defaults
        /// </summary>
        partial void CustomInit();

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "mySINners")]
        public IList<SINner> MySINners { get; set; }

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "myException")]
        public object MyException { get; set; }

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "callSuccess")]
        public bool? CallSuccess { get; set; }

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "errorText")]
        public string ErrorText { get; set; }

    }
}
