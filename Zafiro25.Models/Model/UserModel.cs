namespace Zafiro25.Models.Model
{
	public class UserModel
	{
		public string id { get; set; }
		public string? name { get; set; }
		public string CF { get; set; }
		public List<string> roles { get; set; }
		public UserModel() 
		{
			CF = "0";
			id = Guid.Empty.ToString();
			roles = new List<String>();
		}
		public override string ToString()
		{
			if(null!=name)
			{
				return string.Format("{1} ({0})", CF, name);
			}
			else
			{
				return string.Format("Usuario {0}", CF);
			}
		}
	}
}
