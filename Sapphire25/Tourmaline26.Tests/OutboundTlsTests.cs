using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Tourmaline26.Services.Http;

namespace Tourmaline26.Tests;

public sealed class OutboundTlsTests
{
	[Theory]
	[InlineData("info.trensfm.com", "*.trensfm.com", true)]
	[InlineData("INFO.TRENSFM.COM", "*.trensfm.com", true)]
	[InlineData("trensfm.com", "*.trensfm.com", false)]
	[InlineData("a.b.trensfm.com", "*.trensfm.com", false)]
	[InlineData("info.trensfm.com", "info.trensfm.com", true)]
	[InlineData("info.trensfm.com", "trensfm.com", false)]
	[InlineData("info.trensfm.com", "other.com", false)]
	[InlineData("info.trensfm.com.", "*.trensfm.com.", true)]
	[InlineData("www.tib.org", "*.tib.org", true)]
	[InlineData("tib.org", "*.tib.org", false)]
	[InlineData("tib.org", "tib.org", true)]
	[InlineData("www.tib.org", "*.consorcidetransports.com", false)]
	public void MatchesDnsName_rfc6125_single_label_wildcard(string host, string pattern, bool expected)
	{
		Assert.Equal(expected, OutboundHttp.MatchesDnsName(host, pattern));
	}

	[Fact]
	public void HostNameMatchesCertificate_wildcard_covers_info_host()
	{
		using X509Certificate2 cert = CreateCert("CN=*.trensfm.com", "*.trensfm.com", "trensfm.com");
		Assert.True(OutboundHttp.HostNameMatchesCertificate("info.trensfm.com", cert));
		Assert.True(OutboundHttp.HostNameMatchesCertificate("trensfm.com", cert));
		Assert.False(OutboundHttp.HostNameMatchesCertificate("a.b.trensfm.com", cert));
		Assert.False(OutboundHttp.HostNameMatchesCertificate("213.99.47.36", cert));
	}

	[Fact]
	public void ValidateServerCertificate_accepts_name_mismatch_when_wildcard_covers_host()
	{
		using X509Certificate2 cert = CreateCert("CN=*.trensfm.com", "*.trensfm.com", "trensfm.com");
		bool ok = OutboundHttp.ValidateServerCertificate(
			"info.trensfm.com",
			IPAddress.Parse("213.99.47.36"),
			cert,
			chain: null,
			SslPolicyErrors.RemoteCertificateNameMismatch);
		Assert.True(ok);
	}

	[Fact]
	public void HostNameMatchesCertificate_tib_san_covers_www_even_if_cn_is_other_org()
	{
		// Certificado real de TIB: CN=*.consorcidetransports.com, SAN=*.tib.org + tib.org.
		using X509Certificate2 cert = CreateCert(
			"CN=*.consorcidetransports.com",
			"*.consorcidetransports.com",
			"*.tib.org",
			"tib.org");
		Assert.True(OutboundHttp.HostNameMatchesCertificate("www.tib.org", cert));
		Assert.True(OutboundHttp.HostNameMatchesCertificate("tib.org", cert));
		Assert.False(OutboundHttp.HostNameMatchesCertificate("85.62.90.188", cert));
		Assert.False(OutboundHttp.HostNameMatchesCertificate("www.consorcidetransports.com.example", cert));
	}

	[Fact]
	public void ValidateServerCertificate_accepts_tib_name_mismatch_via_san()
	{
		using X509Certificate2 cert = CreateCert(
			"CN=*.consorcidetransports.com",
			"*.consorcidetransports.com",
			"*.tib.org",
			"tib.org");
		bool ok = OutboundHttp.ValidateServerCertificate(
			"www.tib.org",
			IPAddress.Parse("85.62.90.188"),
			cert,
			chain: null,
			SslPolicyErrors.RemoteCertificateNameMismatch);
		Assert.True(ok);
	}

	[Fact]
	public void ValidateServerCertificate_rejects_unrelated_certificate()
	{
		using X509Certificate2 cert = CreateCert("CN=wifi.operator.example", "wifi.operator.example");
		bool ok = OutboundHttp.ValidateServerCertificate(
			"info.trensfm.com",
			IPAddress.Parse("213.99.47.36"),
			cert,
			chain: null,
			SslPolicyErrors.RemoteCertificateNameMismatch);
		Assert.False(ok);
	}

	[Fact]
	public void ValidateServerCertificate_rejects_chain_errors()
	{
		using X509Certificate2 cert = CreateCert("CN=*.trensfm.com", "*.trensfm.com");
		bool ok = OutboundHttp.ValidateServerCertificate(
			"info.trensfm.com",
			IPAddress.Parse("213.99.47.36"),
			cert,
			chain: null,
			SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateChainErrors);
		Assert.False(ok);
	}

	private static X509Certificate2 CreateCert(string subject, params string[] dnsNames)
	{
		using RSA rsa = RSA.Create(2048);
		var req = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
		if (dnsNames.Length > 0)
		{
			var san = new SubjectAlternativeNameBuilder();
			foreach (string dns in dnsNames)
				san.AddDnsName(dns);
			req.CertificateExtensions.Add(san.Build());
		}
		return req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
	}
}
