﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using EMBC.Suppliers.API.SubmissionModule.ViewModels;

namespace EMBC.Suppliers.API.SubmissionModule.Models.Dynamics
{
    public static class EntityMappers
    {
        public static IEnumerable<SubmissionEntity> MapSubmissions(this IEnumerable<Submission> submissions, string referenceNumber)
        {
            return submissions.Select(s =>
            {
                var supplierInformation = s.Suppliers.FirstOrDefault();
                if (supplierInformation == null) throw new ValidationException($"submission doesn't contain any supplier information");
                if (supplierInformation.Address == null) throw new ValidationException($"supplier's address is missing");
                if (supplierInformation.Address.CityCode == null) throw new ValidationException($"supplier's address doesn't have a city code");
                if (supplierInformation.Address.StateProvinceCode == null) throw new ValidationException($"supplier's address doesn't have a state/province code");
                if (supplierInformation.Address.CountryCode == null) throw new ValidationException($"supplier's address doesn't have a country code");
                var remittanceInformation = s.Suppliers.Count() == 1
                    ? null :
                    s.Suppliers.SingleOrDefault(i => i.ForRemittance);

                var receiptLineItems = s.Receipts.Select(r => (receipt: r, lineItems: s.LineItems.Where(li => li.ReceiptNumber == r.ReceiptNumber)));
                var referralLineItems = s.Referrals.Select(r => (referral: r, lineItems: s.LineItems.Where(li => string.IsNullOrEmpty(li.ReceiptNumber) && li.ReferralNumber == r.ReferralNumber)));

                var receiptAttachments = s.Receipts
                    .Select(r => (receipt: r, attachments: s.Attachments
                        .Where(a => a.ReferralNumber == r.ReferralNumber && (a.Type == AttachmentType.Receipt || a.ReferenceType == AttachmentType.Receipt))));
                var referralAttachments = s.Referrals
                    .Select(r => (referral: r, attachments: s.Attachments
                        .Where(a => a.ReferralNumber == r.ReferralNumber && (a.Type == AttachmentType.Referral || a.ReferenceType == AttachmentType.Referral))));
                var invoiceAttachments = s.Invoices
                    .Select(i => (invoic: i, attachments: s.Attachments
                        .Where(a => a.InvoiceNumber == i.InvoiceNumber && (a.Type == AttachmentType.Invoice || a.ReferenceType == AttachmentType.Invoice))));

                return new SubmissionEntity
                {
                    isInvoice = s.Invoices.Any(),
                    invoiceCollection = s.Invoices.MapInvoices(referenceNumber, supplierInformation, remittanceInformation),
                    referralCollection = s.Referrals.MapReferrals(referenceNumber),
                    lineItemCollection = receiptLineItems.SelectMany(rli => rli.lineItems.MapLineItems(referenceNumber, rli.receipt))
                                             .Concat(referralLineItems.SelectMany(rli => rli.lineItems.MapLineItems(referenceNumber, rli.referral))),
                    documentCollection = s.Attachments.Any()
                                             ? receiptAttachments.SelectMany(ra => ra.attachments.MapAttachments())
                                                 .Concat(referralAttachments.SelectMany(ra => ra.attachments.MapAttachments()))
                                                 .Concat(invoiceAttachments.SelectMany(ra => ra.attachments.MapAttachments()))
                                             : null
                };
            });
        }

        public static IEnumerable<InvoiceEntity> MapInvoices(this IEnumerable<Invoice> invoices, string referenceNumber, SupplierInformation supplierInformation, SupplierInformation supplierRemittanceInformation)
        {
            if (!invoices.Any())
            {
                //dummy invoice for Dynamics when no invoices in submission
                invoices = new[]
                {
                    new Invoice()
                };
            }

            return invoices.Select((i, n) => new InvoiceEntity
            {
                era_invoicedate = i.Date,
                era_invoiceref = i.InvoiceNumber,
                era_referencenumber = referenceNumber,
                era_remitpaymenttootherbusiness = supplierRemittanceInformation != null,
                era_totalgst = i.TotalGST,
                era_totalinvoiceamount = i.TotalAmount,
                era_invoicetype = 174360100, //fire and forget
                era_supplierinvoicenumber = i.InvoiceNumber,
                era_suppliername = supplierInformation.Name,
                era_supplierlegalname = supplierInformation.LegalBusinessName,
                era_storenumber = supplierInformation.Location,
                era_gstnumber = supplierInformation.GstNumber,
                era_addressline1 = supplierInformation.Address.AddressLine1,
                era_addressline2 = supplierInformation.Address.AddressLine2,
                era_city = supplierInformation.Address.City,
                era_RelatedJurisdiction = supplierInformation.Address.CityCode.MapToDynamicsReferencedEntity("era_jurisdictions"),
                era_postalcode = supplierInformation.Address.PostalCode,
                era_province = supplierInformation.Address.StateProvinceCode.MapToDynamicsReferencedEntity("era_provinceterritorieses"),
                era_country = supplierInformation.Address.Country,
                era_legalbusinessname = supplierRemittanceInformation?.LegalBusinessName,
                era_remitcountry = supplierRemittanceInformation?.Address?.Country,
                era_remitcity = supplierRemittanceInformation?.Address?.City,
                era_remitaddress1 = supplierRemittanceInformation?.Address?.AddressLine1,
                era_remitaddress2 = supplierRemittanceInformation?.Address?.AddressLine2,
                era_remitprovincestate = supplierRemittanceInformation?.Address?.StateProvince,
                era_remitpostalcode = supplierRemittanceInformation?.Address?.PostalCode,
                era_contactfirstname = supplierInformation.ContactPerson.FirstName,
                era_contactlastname = supplierInformation.ContactPerson.LastName,
                era_contactemail = supplierInformation.ContactPerson.Email,
                era_contactnumber = supplierInformation.ContactPerson.Phone,
                era_contactfax = supplierInformation.ContactPerson.Fax
            });
        }

        public static IEnumerable<ReferralEntity> MapReferrals(this IEnumerable<Referral> referrals, string referenceNumber)
        {
            return referrals.Select((r, n) => new ReferralEntity
            {
                era_referralnumber = r.ReferralNumber,
                era_totalgst = r.TotalGST,
                era_totalamount = r.TotalAmount,
                era_invoicereference = r.InvoiceNumber,
                era_submissionreference = referenceNumber
            });
        }

        public static IEnumerable<LineItemEntity> MapLineItems(this IEnumerable<LineItem> lineItems, string referenceNumber, Receipt receipt)
        {
            return lineItems.Select((l, n) => new LineItemEntity
            {
                era_SupportsProvided = l.SupportProvided.MapToDynamicsReferencedEntity("era_supports"),
                era_description = l.Description,
                era_gst = l.GST,
                era_amount = l.Amount,
                era_receipt = receipt.ReceiptNumber,
                era_receiptdate = receipt.Date,
                era_referralreference = receipt.ReferralNumber,
                era_submissionreference = referenceNumber
            });
        }

        public static IEnumerable<LineItemEntity> MapLineItems(this IEnumerable<LineItem> lineItems, string referenceNumber, Referral referral)
        {
            return lineItems.Select((l, n) => new LineItemEntity
            {
                era_SupportsProvided = l.SupportProvided.MapToDynamicsReferencedEntity("era_supports"),
                era_description = l.Description,
                era_gst = l.GST,
                era_amount = l.Amount,
                era_receipt = string.Empty,
                era_receiptdate = null,
                era_referralreference = referral.ReferralNumber,
                era_submissionreference = referenceNumber
            });
        }

        public static IEnumerable<AttachmentEntity> MapAttachments(this IEnumerable<Attachment> attachments)
        {
            Func<Attachment, string> subjectMapper = a =>
            {
                switch (a.Type)
                {
                    case AttachmentType.Receipt:
                        return $"inv-{a.InvoiceNumber}";

                    case AttachmentType.Referral:
                        return $"ref-{a.ReferralNumber}";

                    case AttachmentType.Invoice:
                        return $"rec-{a.ReferralNumber}";

                    default:
                        throw new NotSupportedException();
                }
            };
            return attachments.Select((a, n) => new AttachmentEntity
            {
                body = a.Content,
                filename = a.FileName,
                subject = subjectMapper(a)
            });
        }

        private static string MapToDynamicsReferencedEntity(this string value, string entityName)
            => string.IsNullOrWhiteSpace(value) ? null : $"/{entityName}({value})";
    }
}
