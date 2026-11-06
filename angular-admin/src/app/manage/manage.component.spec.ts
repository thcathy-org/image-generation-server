import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ManageComponent } from './manage.component';

describe('ManageComponent', () => {
  let component: ManageComponent;
  let fixture: ComponentFixture<ManageComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [ManageComponent]
    });
    fixture = TestBed.createComponent(ManageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should retire the pending review workflow', () => {
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Pending image review is retired');
    expect(text).not.toContain('Submit Verified');
  });
});
