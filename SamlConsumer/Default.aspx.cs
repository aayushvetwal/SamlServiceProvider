using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Xml;
using System.Xml.Serialization;
using SamlServiceProvider.SamlLibrary;

namespace SamlServiceProvider
{
    public partial class _Default : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {

        }

        protected void btnSubmit_Click(object sender, EventArgs e)
        {
            XmlDocument doc = new XmlDocument();
            XmlElement element = doc.CreateElement("data");
            XmlElement username = doc.CreateElement("username");
            username.InnerText = txtUsername.Text.Trim();
            element.AppendChild(username);
            XmlElement password = doc.CreateElement("password");
            password.InnerText = txtPassword.Text.Trim();
            element.AppendChild(password);
            XmlElement methodname = doc.CreateElement("methodname");
            methodname.InnerText = "getsamltoken";
            element.AppendChild(methodname);
            doc.AppendChild(element);
            
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://localhost:5846/");
            byte[] bytes;
            bytes = System.Text.Encoding.ASCII.GetBytes(ConvertToString(doc));
            request.ContentType = "text/xml; encoding='utf-8'";
            request.ContentLength = bytes.Length;
            request.Method = "POST";
            Stream requestStream = request.GetRequestStream();
            requestStream.Write(bytes, 0, bytes.Length);
            requestStream.Close();
            HttpWebResponse response;

            try
            {
                response = (HttpWebResponse)request.GetResponse();

                XmlDocument xmlResp = new XmlDocument();
                string responseText = string.Empty;
                String samlResponseString = string.Empty;
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        responseText = reader.ReadToEnd();
                    }
                        
                    samlResponseString = Encoding.UTF8.GetString(Convert.FromBase64String(responseText));
                    
                }

                xmlResp.LoadXml(samlResponseString);

                // Validate X509 Certificate Signature TODO

                AssertionType assertion = GetAssertionFromXMLDoc(xmlResp);

                if (assertion.Issuer.Value == ConfigurationManager.AppSettings["CertIssuer"])
                {
                    AssertionData SSOData = new AssertionData(assertion);
                    Dictionary<string, string> attributes = SSOData.SAMLAttributes;
                    foreach (var pair in attributes)
                    {
                        Response.Write(pair.Key + ": " + pair.Value);
                        Response.Write("<br/>");
                    }
                }
            }
            catch (Exception ex)
            {
                Response.Write(ex.Message);
            }
        }

        public string ConvertToString(XmlDocument xmlDoc)
        {
            using (StringWriter sw = new StringWriter())
            {
                using (XmlTextWriter tx = new XmlTextWriter(sw))
                {
                    xmlDoc.WriteTo(tx);
                    string strXmlText = sw.ToString();
                    return strXmlText;
                }
            }
        }

        private AssertionType GetAssertionFromXMLDoc(XmlDocument SAMLXML)
        {
            XmlNamespaceManager ns = new XmlNamespaceManager(SAMLXML.NameTable);
            ns.AddNamespace("saml", "urn:oasis:names:tc:SAML:2.0:assertion");
            XmlElement xeAssertion = SAMLXML.DocumentElement.SelectSingleNode("saml:Assertion", ns) as XmlElement;

            XmlSerializer serializer = new XmlSerializer(typeof(AssertionType));
            AssertionType assertion = (AssertionType)serializer.Deserialize(new XmlNodeReader(xeAssertion));
            return assertion;
        }

    }

    public class AssertionData
    {

        public Dictionary<string, string> SAMLAttributes;
        
        public AssertionData(AssertionType assertion)
        {

            // Find the attribute statement within the assertion
            AttributeStatementType ast = null;

            foreach (StatementAbstractType sat in assertion.Items)
            {
                if (sat.GetType().Equals(typeof(AttributeStatementType)))
                {

                    ast = (AttributeStatementType)sat;

                }
            }
            
            if (ast == null)
            {
                throw new ApplicationException("Invalid SAML Assertion: Missing Attribute Values");
            }

            SAMLAttributes = new Dictionary<string, string>();
            
            // Do what needs to be done to pull specific attributes out for sending on
            // For now assuming this is a simple list of string key and string values

            foreach (AttributeType at in ast.Items)
            {
                SAMLAttributes.Add(at.Name, ((XmlNode[])at.AttributeValue[0])[1].Value);
            }

        }

    }
}