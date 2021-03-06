/// <reference types="cypress" />
var userName = ''

context('User - Service Inactive', () => {
    before(() => {
        cy.visit(Cypress.env('url'));
        cy.wait(3000);
    })

    chatDisabled();
    showingCountdown();
});


function chatDisabled() {
    it('Chat is disabled', () => {
        cy.get('#chatContainer')
            .should('have.class', 'chatDisabled');
    });
}

function showingCountdown() {
    it('Countdown is shown', () => {
        cy.get('#videoFrame')
            .should('not.be.visible');
        cy.get('#noVideoContent')
            .should('be.visible')
            .should('contain', 'Next Service Time');
    });
}