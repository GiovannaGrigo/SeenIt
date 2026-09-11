import { ComponentFixture, TestBed } from '@angular/core/testing';

import { EpisodioModal } from './episodio-modal.component';

describe('EpisodioModal', () => {
  let component: EpisodioModal;
  let fixture: ComponentFixture<EpisodioModal>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [EpisodioModal]
    })
    .compileComponents();

    fixture = TestBed.createComponent(EpisodioModal);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
