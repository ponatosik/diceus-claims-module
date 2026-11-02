import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { AuditLogEntry } from '../../api/models';
import { AUDIT_PAGE_SIZES, AuditStore } from './audit.store';
import { auditEntityLink, auditEventFamily } from './audit-links';

const LINK_TEXT = {
  reserves: 'View reserve',
  parties: 'View party',
  documents: 'View document',
} as const;

/**
 * §11.3 Tab 5 — Audit Log: the append-only event list, newest first, paginated. Read-only by design:
 * there is no edit or delete affordance anywhere (AUD rules).
 */
@Component({
  selector: 'app-claim-audit-tab',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, MatPaginatorModule, MatProgressBarModule, RouterLink],
  providers: [AuditStore],
  templateUrl: './claim-audit-tab.html',
  styleUrl: './claim-audit-tab.scss',
})
export class ClaimAuditTab {
  protected readonly store = inject(AuditStore);
  protected readonly pageSizes = AUDIT_PAGE_SIZES;
  protected readonly family = auditEventFamily;

  protected linkFor(entry: AuditLogEntry) {
    const link = auditEntityLink(entry);
    return link === null
      ? null
      : {
          queryParams: { tab: link.tab, highlight: link.highlight },
          text: LINK_TEXT[link.tab as keyof typeof LINK_TEXT],
        };
  }
}
