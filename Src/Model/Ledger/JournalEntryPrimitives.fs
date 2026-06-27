module Model.Ledger.JournalEntryPrimitives

open System
open NodaTime

type JournalEntryLinePrimitives = {
                accountId: Guid
                amount: decimal
                lineType: string
                memo: string option }

type JournalEntryHeaderPrimitives = {
                description: string
                source: string option
                entryDate: LocalDate
                voidedAt: Instant option }

type JournalEntryExternalReferencePrimitives = {
                financialInstitution: string 
                referenceText: string }

type JournalEntryCommentPrimitives = {
                secondaryJournalEntryId: Guid option
                commentText: string }

type JournalEntryPrimitives = {
                header: JournalEntryHeaderPrimitives
                lines: JournalEntryLinePrimitives list
                externalReferences: JournalEntryExternalReferencePrimitives list
                comments: JournalEntryCommentPrimitives list }

