using System.Xml;

namespace Sapphire2025Server.Expert
{
    /// <summary>
    /// Los archivos XML del proyecto tienen sus propias estructuras.
    /// La idea es que los operadores puedan importar indistintamente cualquier tipo de dato
    /// en XML y el sistema reconozca automáticamente la información que contiene.
    /// </summary>
    public class UniversalXMLImporter
    {
        protected IConfiguration mvarConfiguration; //Objeto que necesito para acceder a la base de datos.
        public UniversalXMLImporter(IConfiguration config)
        {
            mvarConfiguration = config;
        }
        public async Task<string> ImportXML(XmlDocument document)
        {
            XmlElement? raiz = document.DocumentElement;
            if (null == raiz)
                return "No se ha encontrado ningún elemento padre en el documento a importar. Verifique la sintaxis del XML";
            if(raiz.Equals("plan"))
            {
                //Esto es un plan de explotación
                WorkSheetTemplateCollectionImporter auxImportaPlan = new WorkSheetTemplateCollectionImporter(mvarConfiguration);
                return await auxImportaPlan.ImportXML(document);
            }
            else if (raiz.Equals("agentslist"))
            {
                //Lista de Agentes para los gráficos de Excel
                AgentsListImporter auxImportAgents = new AgentsListImporter(mvarConfiguration);
                return await auxImportAgents.ImportXML(document);
            }
            return "Error desconocido en el importador. Póngase en contacto con el equipo de desarrollo.";
        }

    }
}
